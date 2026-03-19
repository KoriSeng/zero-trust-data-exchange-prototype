---
type: Task
task_id: TASK-023
title: "Create Security Testing Suite for Authentication"
owner: STK-001
status: "Not Started"
related_milestone: MS-007
---

## Description

Create a dedicated security-focused test suite that verifies authentication and authorization boundary behaviour — specifically the cases that the existing happy-path k6 tests do **not** cover. The existing tests (`backend-smoke.js`, `backend-api.js`) validate that authenticated requests succeed and that JIT provisioning works. This task fills the gap by testing what the system must **reject**: expired tokens, wrong-audience tokens, cross-organization data access, and token replay.

All new tests should be added to `implementation/k6/` as a new file: `auth-security.js`.

## Implementation Notes

### Scope and Constraints

**Local testing (alg: none mock JWTs):** The existing `generateJWT` helper in `backend-smoke.js` produces unsigned JWTs (`alg: none`) that the local backend accepts because signature validation is disabled in the Lambda. This lets you test claims-based logic (expiry, audience, cross-org isolation) without needing real Cognito tokens — but **only works against a locally running backend**. API Gateway will reject `alg: none` tokens with `401` because it performs real RS256 signature verification.

**Cloud / AWS testing:** To run security tests against the deployed API Gateway endpoint, you need real Cognito `id_token` values. Obtain them via:
```bash
# Authenticate a test user through the Cognito Hosted UI (authorization code flow)
# then exchange the code for tokens — see TASK-022 testing procedure.
# Pass the id_token as an environment variable:
k6 run -e BACKEND_API_URL=https://<api-id>.execute-api.<region>.amazonaws.com \
       -e COGNITO_TOKEN_IDP_A=<real-id-token> \
       implementation/k6/auth-security.js
```

---

### Test Cases to Implement in `implementation/k6/auth-security.js`

#### Test 1 — Expired Token → Expect 401

Manually set `exp` to a timestamp in the past. The Lambda's JwtBearer middleware validates `exp` even though it skips signature validation, so this should be rejected locally. API Gateway also independently enforces `exp` in the cloud.

```javascript
// auth-security.js

import encoding from "k6/encoding";
import http from "k6/http";
import { check, group } from "k6";

function generateExpiredJWT(sub, email, name, cognitoGroup) {
  const header  = { alg: "none", typ: "JWT" };
  const pastTime = Math.floor(Date.now() / 1000) - 7200; // 2 hours ago

  const payload = {
    sub, email, name,
    "cognito:username": sub,
    "cognito:groups": [cognitoGroup],
    iss: "https://cognito-idp.ap-southeast-1.amazonaws.com/ap-southeast-1_xxxxx",
    aud: "test-client-id",
    token_use: "id",
    auth_time: pastTime,
    iat: pastTime,
    exp: pastTime,   // ← already expired
  };

  const h = encoding.b64encode(JSON.stringify(header),  "rawurl");
  const p = encoding.b64encode(JSON.stringify(payload), "rawurl");
  return `${h}.${p}.`;
}

export default function () {
  const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";

  group("SEC-001: Expired token is rejected with 401", function () {
    const expiredJwt = generateExpiredJWT(
      "IDP-A_A-001",
      "a.alex@org-a.example.com",
      "Alex Kim",
      "ap-southeast-1_xxxxx_IDP-A"
    );

    const res = http.get(`${BASE_URL}/me`, {
      headers: { Authorization: `Bearer ${expiredJwt}` },
    });

    check(res, {
      "SEC-001 expired token returns 401": (r) => r.status === 401,
    });
  });
```

#### Test 2 — Wrong Audience → Expect 401

Set `aud` to a value that does not match the configured app client ID. JwtBearer's `ValidateAudience = true` should reject this.

```javascript
  group("SEC-002: Token with wrong audience is rejected with 401", function () {
    const header  = { alg: "none", typ: "JWT" };
    const now     = Math.floor(Date.now() / 1000);
    const payload = {
      sub: "IDP-A_A-001",
      email: "a.alex@org-a.example.com",
      name: "Alex Kim",
      "cognito:username": "IDP-A_A-001",
      "cognito:groups": ["ap-southeast-1_xxxxx_IDP-A"],
      iss: "https://cognito-idp.ap-southeast-1.amazonaws.com/ap-southeast-1_xxxxx",
      aud: "WRONG-CLIENT-ID",   // ← incorrect audience
      token_use: "id",
      iat: now, exp: now + 3600,
    };

    const h = encoding.b64encode(JSON.stringify(header),  "rawurl");
    const p = encoding.b64encode(JSON.stringify(payload), "rawurl");
    const wrongAudJwt = `${h}.${p}.`;

    const res = http.get(`${BASE_URL}/me`, {
      headers: { Authorization: `Bearer ${wrongAudJwt}` },
    });

    check(res, {
      "SEC-002 wrong audience returns 401": (r) => r.status === 401,
    });
  });
```

#### Test 3 — Cross-Organization Access → Expect 403 or Empty List

An IDP-A user (Requester, ORG-A) calls `GET /requests/pending`, which is intended for DataOwners in ORG-B. The endpoint should return either `403 Forbidden` or an empty array `[]` — it must **not** return ORG-B's pending requests.

```javascript
  group("SEC-003: IDP-A user cannot see IDP-B pending approvals", function () {
    // Generate a valid IDP-A token
    const idpAUser = {
      sub: "IDP-A_A-001", email: "a.alex@org-a.example.com", name: "Alex Kim",
      cognitoGroup: "ap-southeast-1_xxxxx_IDP-A",
    };
    const jwt = generateValidJWT(idpAUser); // use the standard generateJWT helper

    const res = http.get(`${BASE_URL}/requests/pending`, {
      headers: { Authorization: `Bearer ${jwt}`, "Content-Type": "application/json" },
    });

    check(res, {
      "SEC-003 cross-org access is denied or returns empty list": (r) => {
        if (r.status === 403) return true;
        if (r.status === 200) {
          try {
            const body = JSON.parse(r.body);
            // Must not contain requests belonging to ORG-B
            return Array.isArray(body) && body.length === 0;
          } catch (_) { return false; }
        }
        return false;
      },
    });
  });
```

#### Test 4 — No Token → Expect 401

Confirms the baseline: a completely missing Authorization header is rejected.

```javascript
  group("SEC-004: Request with no token returns 401", function () {
    const res = http.get(`${BASE_URL}/me`);  // no Authorization header

    check(res, {
      "SEC-004 missing token returns 401": (r) => r.status === 401,
    });
  });
```

#### Test 5 — Token Replay After Simulated Logout

Simulates token replay by reusing a token after the client has "logged out" (cleared its local session). Because the backend is stateless — it does not maintain a server-side session blocklist — this test documents the expected behaviour for a POC: the token remains valid until its `exp` claim elapses. The test records this outcome and serves as a baseline for a future token revocation mechanism.

```javascript
  group("SEC-005: Token replay (stateless — token valid until exp)", function () {
    // Step 1: make a successful request to confirm the token works
    const jwt = generateValidJWT({
      sub: "IDP-A_A-002", email: "a.sam@org-a.example.com", name: "Sam Lee",
      cognitoGroup: "ap-southeast-1_xxxxx_IDP-A",
    });

    const firstRes = http.get(`${BASE_URL}/me`, {
      headers: { Authorization: `Bearer ${jwt}` },
    });
    check(firstRes, { "SEC-005a first request succeeds": (r) => r.status === 200 });

    // Step 2: simulate logout (client discards token — server has no blocklist)
    // Step 3: reuse the same token — for a POC this succeeds; document the gap
    const replayRes = http.get(`${BASE_URL}/me`, {
      headers: { Authorization: `Bearer ${jwt}` },
    });
    check(replayRes, {
      // For a POC without a revocation list, 200 is the expected (documented) outcome.
      // A production system would need Cognito token revocation or a Redis blocklist.
      "SEC-005b replayed token accepted (POC gap — no revocation list)": (r) =>
        r.status === 200,
    });
  });
}
```

### Running the Security Tests

```bash
# Against local backend (uses alg:none mock JWTs)
k6 run implementation/k6/auth-security.js

# Against deployed AWS environment (requires real Cognito tokens)
k6 run \
  -e BACKEND_API_URL=https://<api-id>.execute-api.<region>.amazonaws.com \
  -e COGNITO_TOKEN_IDP_A=<real-cognito-id-token-for-idp-a-user> \
  implementation/k6/auth-security.js
```

### Coverage Gap Summary

| Security Concern | Covered By | Gap? |
|---|---|---|
| Valid auth flow | `backend-smoke.js`, `backend-api.js` | No gap |
| JIT provisioning | `backend-smoke.js` | No gap |
| Identity isolation (cross-IdP) | `backend-smoke.js` | No gap |
| Expired token rejected | `auth-security.js` (SEC-001) | **This task** |
| Wrong audience rejected | `auth-security.js` (SEC-002) | **This task** |
| Cross-org data access blocked | `auth-security.js` (SEC-003) | **This task** |
| Missing token rejected | `auth-security.js` (SEC-004) | **This task** |
| Token replay (stateless) | `auth-security.js` (SEC-005) | **This task** (documents POC limitation) |
| Token revocation / blocklist | None | Out of scope for POC |
| PKCE / auth code interception | None | Out of scope for POC |

## Definition of Done

- [ ] `implementation/k6/auth-security.js` created with all five test groups
- [ ] SEC-001: expired token test passes (local backend returns `401`)
- [ ] SEC-002: wrong-audience token test passes (local backend returns `401`)
- [ ] SEC-003: cross-org access test passes (IDP-A user gets `403` or `[]` from `/requests/pending`)
- [ ] SEC-004: missing token test passes (local backend returns `401`)
- [ ] SEC-005: token replay test runs and outcome is documented (POC gap acknowledged in test output)
- [ ] All tests runnable with `k6 run implementation/k6/auth-security.js` against local backend
- [ ] README or inline comments note which tests require real Cognito tokens for cloud execution
- [ ] Test results exported/captured as evidence for MS-007 validation
