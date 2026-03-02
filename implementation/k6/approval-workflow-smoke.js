import http from "k6/http";
import { check, group } from "k6";
import encoding from "k6/encoding";

/**
 * Approval Workflow Smoke Test
 *
 * Exercises the full data-access-request lifecycle against the live backend:
 *
 *   GET  /requests/my                  — requester lists own requests
 *   GET  /requests/pending             — data owner lists pending requests
 *   GET  /requests/{id}                — authorised and unauthorised read
 *   POST /requests/{id}/approve        — data owner approves → OTP issued
 *   POST /requests/{id}/redeem         — requester redeems OTP → pre-signed URLs
 *   POST /requests                     — submit a new request
 *   POST /requests/{id}/deny           — data owner denies a request
 *
 * Prerequisites
 *   • Backend running with DatabaseSeederHostedService initialised (fresh seed)
 *   • LocalStack running with zero-trust-data bucket and sample objects seeded
 *     (implementation/backend/scripts/localstack-init.sh)
 *
 * Run:
 *   k6 run implementation/k6/approval-workflow-smoke.js \
 *       -e BACKEND_API_URL=http://localhost:5000
 */
export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate>0.9"], // 90 % of checks must pass
  },
};

const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";

// ---------------------------------------------------------------------------
// Test users — must match seed data in DatabaseSeederHostedService
// IDP-A ↔ ORG-A (Data Custodian / Requesters)
// IDP-B ↔ ORG-B (Data Readers / Data Owners)
// ---------------------------------------------------------------------------
const TEST_USERS = {
  requesterA001: {
    sub: "IDP-A_A-001",
    email: "a.alex@org-a.example.com",
    name: "Alex Kim",
    cognitoGroup: "ap-southeast-1_xxxxx_IDP-A",
  },
  // A different ORG-A user — should NOT be able to see ORG-B requests
  requesterA002: {
    sub: "IDP-A_A-002",
    email: "a.sam@org-a.example.com",
    name: "Sam Lee",
    cognitoGroup: "ap-southeast-1_xxxxx_IDP-A",
  },
  dataOwnerB001: {
    sub: "IDP-B_B-001",
    email: "b.alex@org-b.example.com",
    name: "Alex Kim",
    cognitoGroup: "ap-southeast-1_yyyyy_IDP-B",
  },
};

/**
 * Generate an unsigned JWT (alg:none) suitable for local testing.
 * The backend is configured to skip signature validation.
 */
function generateJWT(user) {
  const header = { alg: "none", typ: "JWT" };
  const now = Math.floor(Date.now() / 1000);

  const payload = {
    sub: user.sub,
    email: user.email,
    email_verified: true,
    name: user.name,
    "cognito:username": user.sub,
    "cognito:groups": [user.cognitoGroup],
    identities: [
      {
        userId: user.sub.split("_")[1],
        providerName: user.sub.split("_")[0],
        providerType: "OIDC",
        issuer: user.sub.startsWith("IDP-A")
          ? "http://localhost:9001"
          : "http://localhost:9002",
        primary: "true",
      },
    ],
    iss: "https://cognito-idp.ap-southeast-1.amazonaws.com/ap-southeast-1_xxxxx",
    aud: "test-client-id",
    token_use: "id",
    auth_time: now,
    iat: now,
    exp: now + 3600,
  };

  const headerB64 = encoding.b64encode(JSON.stringify(header), "rawurl");
  const payloadB64 = encoding.b64encode(JSON.stringify(payload), "rawurl");
  return `${headerB64}.${payloadB64}.`;
}

function authHeaders(jwt) {
  return { Authorization: `Bearer ${jwt}`, "Content-Type": "application/json" };
}

function parseBody(res) {
  try {
    return JSON.parse(res.body);
  } catch (_) {
    return null;
  }
}

// ---------------------------------------------------------------------------
export default function () {
  console.log(`\nApproval Workflow Smoke Test — ${BASE_URL}`);
  console.log("===========================================\n");

  const jwtRequester = generateJWT(TEST_USERS.requesterA001);
  const jwtUnrelated = generateJWT(TEST_USERS.requesterA002);
  const jwtDataOwner = generateJWT(TEST_USERS.dataOwnerB001);

  // State shared across groups
  let seededId = null; // internal UUID of REQ-2024-001 (approve/redeem test)
  let denyId = null;   // internal UUID of REQ-2024-002 (deny test, seeded independently)
  let otp = null; // one-time password from approve response

  // =========================================================================
  // Section 1 — Discovery
  // =========================================================================

  group("GET /requests/my — requester lists own requests", function () {
    const res = http.get(`${BASE_URL}/requests/my`, {
      headers: authHeaders(jwtRequester),
    });

    const passed = check(res, {
      "returns 200": (r) => r.status === 200,
      "body is an array": (r) => Array.isArray(parseBody(r)),
      "seeded REQ-2024-001 is present": (r) => {
        const body = parseBody(r);
        return (
          Array.isArray(body) &&
          body.some((req) => req.requestId === "REQ-2024-001")
        );
      },
    });
    console.log(passed ? "✓ GET /requests/my" : "✗ GET /requests/my");
  });

  group(
    "GET /requests/pending — data owner finds seeded pending request",
    function () {
      const res = http.get(`${BASE_URL}/requests/pending`, {
        headers: authHeaders(jwtDataOwner),
      });

      const passed = check(res, {
        "returns 200": (r) => r.status === 200,
        "body is an array": (r) => Array.isArray(parseBody(r)),
        "at least two pending requests": (r) => {
          const body = parseBody(r);
          return Array.isArray(body) && body.length >= 2;
        },
        "seeded REQ-2024-001 is present": (r) => {
          const body = parseBody(r);
          return (
            Array.isArray(body) &&
            body.some((req) => req.requestId === "REQ-2024-001")
          );
        },
        "seeded REQ-2024-002 is present": (r) => {
          const body = parseBody(r);
          return (
            Array.isArray(body) &&
            body.some((req) => req.requestId === "REQ-2024-002")
          );
        },
      });

      if (passed) {
        const body = parseBody(res);
        const seeded = body.find((r) => r.requestId === "REQ-2024-001");
        const forDeny = body.find((r) => r.requestId === "REQ-2024-002");
        seededId = seeded?.id ?? null;
        denyId = forDeny?.id ?? null;
        console.log(
          `✓ GET /requests/pending — captured ${seeded?.requestId} (id: ${seededId})`,
        );
        console.log(
          `✓ GET /requests/pending — captured ${forDeny?.requestId} (id: ${denyId})`,
        );
      } else {
        console.log("✗ GET /requests/pending");
      }
    },
  );

  if (!seededId || !denyId) {
    console.log(
      "✗ Cannot find seeded requests — aborting tests (seededId=" + seededId + " denyId=" + denyId + ")",
    );
    return;
  }

  // =========================================================================
  // Section 2 — Read-access controls
  // =========================================================================

  group("GET /requests/{id} — requester can view own request", function () {
    const res = http.get(`${BASE_URL}/requests/${seededId}`, {
      headers: authHeaders(jwtRequester),
    });
    const passed = check(res, {
      "returns 200": (r) => r.status === 200,
      "requestId matches": (r) => parseBody(r)?.requestId === "REQ-2024-001",
      "status is PendingOwnerApproval": (r) =>
        parseBody(r)?.status === "PendingOwnerApproval",
    });
    console.log(
      passed
        ? "✓ GET /requests/{id} (requester)"
        : "✗ GET /requests/{id} (requester)",
    );
  });

  group("GET /requests/{id} — data owner can view request", function () {
    const res = http.get(`${BASE_URL}/requests/${seededId}`, {
      headers: authHeaders(jwtDataOwner),
    });
    const passed = check(res, {
      "returns 200": (r) => r.status === 200,
    });
    console.log(
      passed
        ? "✓ GET /requests/{id} (data owner)"
        : "✗ GET /requests/{id} (data owner)",
    );
  });

  group("GET /requests/{id} — unrelated user is forbidden", function () {
    // requesterA002 belongs to ORG-A but is not the requester of this request
    const res = http.get(`${BASE_URL}/requests/${seededId}`, {
      headers: authHeaders(jwtUnrelated),
    });
    const passed = check(res, {
      "unrelated user gets 403": (r) => r.status === 403,
    });
    console.log(
      passed
        ? "✓ GET /requests/{id} — unrelated user correctly forbidden"
        : "✗ GET /requests/{id} — unrelated user should be forbidden",
    );
  });

  // =========================================================================
  // Section 3 — Approve: wrong actor
  // =========================================================================

  group(
    "POST /requests/{id}/approve — requester cannot approve (403)",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/approve`,
        JSON.stringify({ approved: true, comments: "Should be rejected" }),
        { headers: authHeaders(jwtRequester) },
      );
      const passed = check(res, {
        "requester correctly gets 403": (r) => r.status === 403,
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/approve — requester blocked"
          : "✗ POST /requests/{id}/approve — requester should be blocked",
      );
    },
  );

  // =========================================================================
  // Section 4 — Happy-path approval
  // =========================================================================

  group(
    "POST /requests/{id}/approve — data owner approves, OTP issued",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/approve`,
        JSON.stringify({
          approved: true,
          comments: "Approved for ML training",
        }),
        { headers: authHeaders(jwtDataOwner) },
      );
      const passed = check(res, {
        "returns 200": (r) => r.status === 200,
        "status transitions to OtpSent": (r) =>
          parseBody(r)?.status === "OtpSent",
        "otp is a 6-digit string": (r) => {
          const body = parseBody(r);
          return typeof body?.otp === "string" && /^\d{6}$/.test(body.otp);
        },
        "otpExpiresAt is in the future": (r) => {
          const body = parseBody(r);
          return body?.otpExpiresAt && new Date(body.otpExpiresAt) > new Date();
        },
      });

      if (passed) {
        otp = parseBody(res)?.otp;
        console.log(`✓ POST /requests/{id}/approve — OTP: ${otp}`);
      } else {
        console.log("✗ POST /requests/{id}/approve");
      }
    },
  );

  group(
    "POST /requests/{id}/approve — duplicate approval rejected (400)",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/approve`,
        JSON.stringify({ approved: true }),
        { headers: authHeaders(jwtDataOwner) },
      );
      const passed = check(res, {
        "second approval returns 400": (r) => r.status === 400,
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/approve — duplicate blocked"
          : "✗ POST /requests/{id}/approve — duplicate should be blocked",
      );
    },
  );

  if (!otp) {
    console.log("✗ OTP not captured — aborting redemption tests");
    return;
  }

  // =========================================================================
  // Section 5 — Redeem: wrong actor and wrong OTP
  // =========================================================================

  group("POST /requests/{id}/redeem — wrong OTP returns 400", function () {
    const res = http.post(
      `${BASE_URL}/requests/${seededId}/redeem`,
      JSON.stringify({ otp: "000000" }),
      { headers: authHeaders(jwtRequester) },
    );
    const passed = check(res, {
      "wrong OTP returns 400": (r) => r.status === 400,
      "error message is present": (r) =>
        typeof parseBody(r)?.error === "string",
    });
    console.log(
      passed
        ? "✓ POST /requests/{id}/redeem — wrong OTP blocked"
        : "✗ POST /requests/{id}/redeem — wrong OTP check failed",
    );
  });

  group(
    "POST /requests/{id}/redeem — data owner cannot redeem (403)",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/redeem`,
        JSON.stringify({ otp }),
        { headers: authHeaders(jwtDataOwner) },
      );
      const passed = check(res, {
        "data owner correctly gets 403": (r) => r.status === 403,
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/redeem — data owner blocked"
          : "✗ POST /requests/{id}/redeem — data owner should be blocked",
      );
    },
  );

  // =========================================================================
  // Section 6 — Happy-path redemption
  // =========================================================================

  group(
    "POST /requests/{id}/redeem — requester redeems OTP, gets pre-signed URLs",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/redeem`,
        JSON.stringify({ otp }),
        { headers: authHeaders(jwtRequester) },
      );
      const passed = check(res, {
        "returns 200": (r) => r.status === 200,
        "status transitions to Redeemed": (r) =>
          parseBody(r)?.status === "Redeemed",
        "presignedUrls is an object": (r) => {
          const body = parseBody(r);
          return (
            body?.presignedUrls !== null &&
            typeof body?.presignedUrls === "object"
          );
        },
        "presignedUrls contains data-001.csv": (r) =>
          "samples/data-001.csv" in (parseBody(r)?.presignedUrls ?? {}),
        "presignedUrls contains data-002.csv": (r) =>
          "samples/data-002.csv" in (parseBody(r)?.presignedUrls ?? {}),
        "expiresAt is in the future": (r) => {
          const body = parseBody(r);
          return body?.expiresAt && new Date(body.expiresAt) > new Date();
        },
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/redeem — OTP redeemed, URLs issued"
          : "✗ POST /requests/{id}/redeem",
      );
    },
  );

  group(
    "POST /requests/{id}/redeem — double-redeem rejected (400)",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${seededId}/redeem`,
        JSON.stringify({ otp }),
        { headers: authHeaders(jwtRequester) },
      );
      const passed = check(res, {
        "double-redeem returns 400": (r) => r.status === 400,
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/redeem — double-redeem blocked"
          : "✗ POST /requests/{id}/redeem — double-redeem should be blocked",
      );
    },
  );

  // =========================================================================
  // Section 7 — POST /requests (standalone verification)
  // Requires LocalStack S3 to be running with 'samples/data-001.csv' seeded.
  // This section does NOT gate the deny tests below.
  // =========================================================================

  group("POST /requests — requester submits a new request", function () {
    const res = http.post(
      `${BASE_URL}/requests`,
      JSON.stringify({
        datasetId: "dataset-genomic-2024",
        datasetName: "Genomic Research Dataset 2024",
        objectKeys: ["samples/data-001.csv"],
        purpose: "Secondary analysis validation",
        dataOwnerOrg: "ORG-B",
      }),
      { headers: authHeaders(jwtRequester) },
    );

    const passed = check(res, {
      "returns 201": (r) => r.status === 201,
      "response has id (UUID)": (r) => typeof parseBody(r)?.id === "string",
      "response has requestId": (r) =>
        typeof parseBody(r)?.requestId === "string",
      "status is Submitted or PendingOwnerApproval": (r) => {
        const s = parseBody(r)?.status;
        return s === "Submitted" || s === "PendingOwnerApproval";
      },
    });

    if (passed) {
      console.log(
        `✓ POST /requests — created ${parseBody(res)?.requestId} (id: ${parseBody(res)?.id})`,
      );
    } else {
      console.log(
        "✗ POST /requests — check LocalStack S3 is running and objects are seeded (this does not affect the deny tests below)",
      );
    }
  });

  // =========================================================================
  // Section 8 — Deny flow (uses seeded REQ-2024-002, independent of Section 7)
  // =========================================================================

  group("POST /requests/{id}/deny — requester cannot deny (403)", function () {
    const res = http.post(
      `${BASE_URL}/requests/${denyId}/deny`,
      JSON.stringify({ reason: "Self-denial attempt" }),
      { headers: authHeaders(jwtRequester) },
    );
    const passed = check(res, {
      "requester correctly gets 403": (r) => r.status === 403,
    });
    console.log(
      passed
        ? "✓ POST /requests/{id}/deny — requester blocked"
        : "✗ POST /requests/{id}/deny — requester should be blocked",
    );
  });

  group("POST /requests/{id}/deny — data owner denies request", function () {
    const res = http.post(
      `${BASE_URL}/requests/${denyId}/deny`,
      JSON.stringify({ reason: "Data not suitable for the stated purpose" }),
      { headers: authHeaders(jwtDataOwner) },
    );
    const body = parseBody(res);
    const passed = check(res, {
      "returns 200": (r) => r.status === 200,
      "status is Denied": (r) => body?.status === "Denied",
    });
    console.log(
      passed
        ? "✓ POST /requests/{id}/deny — request denied"
        : "✗ POST /requests/{id}/deny",
    );
  });

  group(
    "POST /requests/{id}/deny — duplicate denial rejected (400)",
    function () {
      const res = http.post(
        `${BASE_URL}/requests/${denyId}/deny`,
        JSON.stringify({ reason: "Duplicate attempt" }),
        { headers: authHeaders(jwtDataOwner) },
      );
      const passed = check(res, {
        "second deny returns 400": (r) => r.status === 400,
      });
      console.log(
        passed
          ? "✓ POST /requests/{id}/deny — duplicate blocked"
          : "✗ POST /requests/{id}/deny — duplicate should be blocked",
      );
    },
  );

  // =========================================================================
  // Section 9 — Audit Trail Verification
  // =========================================================================

  group(
    "GET /requests/{id}/audit — requester views audit trail",
    function () {
      const res = http.get(`${BASE_URL}/requests/${seededId}/audit`, {
        headers: authHeaders(jwtRequester),
      });
      const body = parseBody(res);
      const passed = check(res, {
        "returns 200": (r) => r.status === 200,
        "body is an array": (r) => Array.isArray(parseBody(r)),
        "at least one audit event present": (r) => {
          const b = parseBody(r);
          return Array.isArray(b) && b.length >= 1;
        },
        "REQUEST_APPROVED event is present": (r) => {
          const b = parseBody(r);
          return (
            Array.isArray(b) &&
            b.some((e) => e.eventType === "REQUEST_APPROVED")
          );
        },
        "OTP_REDEEMED event is present": (r) => {
          const b = parseBody(r);
          return (
            Array.isArray(b) &&
            b.some((e) => e.eventType === "OTP_REDEEMED")
          );
        },
      });
      console.log(
        passed
          ? `✓ GET /requests/{id}/audit — ${Array.isArray(body) ? body.length : 0} event(s) returned`
          : "✗ GET /requests/{id}/audit — requester",
      );
    },
  );

  group(
    "GET /requests/{id}/audit — data owner can view audit trail",
    function () {
      const res = http.get(`${BASE_URL}/requests/${seededId}/audit`, {
        headers: authHeaders(jwtDataOwner),
      });
      const passed = check(res, {
        "data owner gets 200": (r) => r.status === 200,
        "body is an array": (r) => Array.isArray(parseBody(r)),
      });
      console.log(
        passed
          ? "✓ GET /requests/{id}/audit — data owner access allowed"
          : "✗ GET /requests/{id}/audit — data owner should have access",
      );
    },
  );

  group(
    "GET /requests/{id}/audit — unrelated user is forbidden",
    function () {
      const res = http.get(`${BASE_URL}/requests/${seededId}/audit`, {
        headers: authHeaders(jwtUnrelated),
      });
      const passed = check(res, {
        "unrelated user gets 403": (r) => r.status === 403,
      });
      console.log(
        passed
          ? "✓ GET /requests/{id}/audit — unrelated user correctly forbidden"
          : "✗ GET /requests/{id}/audit — unrelated user should be forbidden",
      );
    },
  );

  group(
    "GET /requests/{id}/audit — deny trail includes REQUEST_DENIED",
    function () {
      const res = http.get(`${BASE_URL}/requests/${denyId}/audit`, {
        headers: authHeaders(jwtDataOwner),
      });
      const passed = check(res, {
        "returns 200": (r) => r.status === 200,
        "REQUEST_DENIED event is present": (r) => {
          const b = parseBody(r);
          return (
            Array.isArray(b) &&
            b.some((e) => e.eventType === "REQUEST_DENIED")
          );
        },
      });
      console.log(
        passed
          ? "✓ GET /requests/{id}/audit — REQUEST_DENIED captured in deny trail"
          : "✗ GET /requests/{id}/audit — deny trail",
      );
    },
  );

  console.log("\n========================================");
  console.log("Approval Workflow Smoke Test Complete");
  console.log("========================================");
}
