import http from "k6/http";
import { check, group } from "k6";
import encoding from "k6/encoding";

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate>0.95"],
  },
};

const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";
const TEST_ISSUER =
  __ENV.TEST_JWT_ISSUER ||
  "https://cognito-idp.ap-southeast-1.amazonaws.com/ap-southeast-1_xxxxx";
const TEST_AUDIENCE = __ENV.TEST_JWT_AUDIENCE || "test-client-id";
const TEST_GROUP_IDP_A =
  __ENV.TEST_COGNITO_GROUP_IDP_A || "ap-southeast-1_xxxxx_IDP-A";
const COGNITO_TOKEN_IDP_A = __ENV.COGNITO_TOKEN_IDP_A || "";

function buildUnsignedJwt(overrides = {}) {
  const header = { alg: "none", typ: "JWT" };
  const now = Math.floor(Date.now() / 1000);

  const payload = {
    sub: "IDP-A_A-001",
    email: "a.alex@org-a.example.com",
    name: "Alex Kim",
    "cognito:username": "IDP-A_A-001",
    "cognito:groups": [TEST_GROUP_IDP_A],
    iss: TEST_ISSUER,
    aud: TEST_AUDIENCE,
    token_use: "id",
    auth_time: now,
    iat: now,
    exp: now + 3600,
    ...overrides,
  };

  const h = encoding.b64encode(JSON.stringify(header), "rawurl");
  const p = encoding.b64encode(JSON.stringify(payload), "rawurl");
  return `${h}.${p}.`;
}

function authHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

function parseJson(body) {
  try {
    return JSON.parse(body);
  } catch (_) {
    return null;
  }
}

function idpAToken() {
  if (COGNITO_TOKEN_IDP_A) {
    return COGNITO_TOKEN_IDP_A;
  }

  return buildUnsignedJwt();
}

export default function () {
  console.log(`Running auth security baseline against ${BASE_URL}`);
  if (BASE_URL.startsWith("https://") && !COGNITO_TOKEN_IDP_A) {
    console.log(
      "No COGNITO_TOKEN_IDP_A supplied. Cloud runs should set a real Cognito token for SEC-003/SEC-005.",
    );
  }

  group("SEC-001: Expired token is rejected with 401", function () {
    const pastTime = Math.floor(Date.now() / 1000) - 7200;
    const expiredJwt = buildUnsignedJwt({
      auth_time: pastTime,
      iat: pastTime,
      exp: pastTime,
    });

    const res = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(expiredJwt),
    });

    check(res, {
      "SEC-001 expired token returns 401": (r) => r.status === 401,
    });
  });

  group("SEC-002: Wrong audience token is rejected with 401", function () {
    const wrongAudienceJwt = buildUnsignedJwt({
      aud: "WRONG-CLIENT-ID",
    });

    const res = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(wrongAudienceJwt),
    });

    check(res, {
      "SEC-002 wrong audience returns 401": (r) => r.status === 401,
    });
  });

  group("SEC-003: Cross-organization boundary is enforced", function () {
    const res = http.get(`${BASE_URL}/requests/pending`, {
      headers: authHeaders(idpAToken()),
    });

    check(res, {
      "SEC-003 IDP-A cannot access IDP-B pending approvals": (r) => {
        if (r.status === 403) {
          return true;
        }

        if (r.status === 200) {
          const data = parseJson(r.body);
          return Array.isArray(data) && data.length === 0;
        }

        return false;
      },
    });
  });

  group("SEC-004: Missing token is rejected with 401", function () {
    const res = http.get(`${BASE_URL}/me`);

    check(res, {
      "SEC-004 missing token returns 401": (r) => r.status === 401,
    });
  });

  group("SEC-005: Token replay note (POC stateless behavior)", function () {
    const token = idpAToken();
    const firstRes = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(token),
    });
    const replayRes = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(token),
    });

    check(firstRes, {
      "SEC-005a first request succeeds": (r) => r.status === 200,
    });
    check(replayRes, {
      "SEC-005b replayed token still accepted (POC gap)": (r) => r.status === 200,
    });
  });
}
