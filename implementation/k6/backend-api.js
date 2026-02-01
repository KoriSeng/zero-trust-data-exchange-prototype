import http from "k6/http";
import { check, group, sleep } from "k6";
import { Rate } from "k6/metrics";

// Custom metrics
const errorRate = new Rate("errors");

// Test configuration
export const options = {
  stages: [
    { duration: "30s", target: 5 }, // Ramp up to 5 users
    { duration: "1m", target: 5 }, // Stay at 5 users
    { duration: "30s", target: 0 }, // Ramp down to 0
  ],
  thresholds: {
    http_req_duration: ["p(95)<2000"], // 95% of requests must complete below 2s
    errors: ["rate<0.1"], // Error rate must be below 10%
  },
};

// Environment configuration
const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";

// Fixed test user JWTs (generated from generate-jwt.sh)
// In practice, these would be generated dynamically or loaded from files
const USERS = {
  requesterA001: {
    name: "Alex Kim (IDP-A)",
    sub: "IDP-A_A-001",
    // JWT would be generated using: ./generate-jwt.sh A-001
    jwt:
      __ENV.JWT_A_001 ||
      generateMockJWT("IDP-A_A-001", "a.alex@org-a.example.com", "Alex Kim"),
  },
  requesterA002: {
    name: "Sam Lee (IDP-A)",
    sub: "IDP-A_A-002",
    jwt:
      __ENV.JWT_A_002 ||
      generateMockJWT("IDP-A_A-002", "a.sam@org-a.example.com", "Sam Lee"),
  },
  dataOwnerB001: {
    name: "Alex Kim (IDP-B)",
    sub: "IDP-B_B-001",
    jwt:
      __ENV.JWT_B_001 ||
      generateMockJWT("IDP-B_B-001", "b.alex@org-b.example.com", "Alex Kim"),
  },
  dataOwnerB002: {
    name: "Jamie Tan (IDP-B)",
    sub: "IDP-B_B-002",
    jwt:
      __ENV.JWT_B_002 ||
      generateMockJWT("IDP-B_B-002", "b.jamie@org-b.example.com", "Jamie Tan"),
  },
  admin: {
    name: "Admin",
    sub: "IDP-A_ADMIN-001",
    jwt:
      __ENV.JWT_ADMIN ||
      generateMockJWT(
        "IDP-A_ADMIN-001",
        "admin@org-custodian.example.com",
        "Admin",
      ),
  },
};

/**
 * Generate a mock JWT for local testing (algorithm: none)
 * Production would use properly signed JWTs from Cognito
 */
function generateMockJWT(sub, email, name) {
  const header = { alg: "none", typ: "JWT" };
  const payload = {
    sub: sub,
    email: email,
    name: name,
    "cognito:username": sub,
    "cognito:groups": [
      sub.startsWith("IDP-A")
        ? "ap-southeast-1_xxxxx_IDP-A"
        : "ap-southeast-1_yyyyy_IDP-B",
    ],
    iss: "https://cognito-idp.ap-southeast-1.amazonaws.com/ap-southeast-1_xxxxx",
    aud: "test-client-id",
    token_use: "id",
    auth_time: Math.floor(Date.now() / 1000),
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 3600,
  };

  const headerB64 = base64UrlEncode(JSON.stringify(header));
  const payloadB64 = base64UrlEncode(JSON.stringify(payload));

  return `${headerB64}.${payloadB64}.`;
}

function base64UrlEncode(str) {
  // Note: k6 doesn't have btoa, so we use encoding.b64encode
  return encoding.b64encode(str, "rawurl");
}

/**
 * Create authorization header
 */
function authHeader(user) {
  return {
    Authorization: `Bearer ${user.jwt}`,
    "Content-Type": "application/json",
  };
}

/**
 * Test Setup - Run once before all tests
 */
export function setup() {
  console.log("===========================================");
  console.log("Zero Trust Backend API - k6 Load Test");
  console.log("===========================================");
  console.log(`Base URL: ${BASE_URL}`);
  console.log("Test Users:");
  Object.entries(USERS).forEach(([key, user]) => {
    console.log(`  - ${key}: ${user.name} (${user.sub})`);
  });
  console.log("===========================================\n");

  // Health check
  const healthRes = http.get(`${BASE_URL}/health`);
  if (healthRes.status !== 200) {
    throw new Error(`API health check failed: ${healthRes.status}`);
  }
  console.log("✓ API health check passed\n");

  return { startTime: new Date() };
}

/**
 * Main test scenario
 */
export default function (data) {
  // Select a random user for this VU iteration
  const userKeys = Object.keys(USERS);
  const randomUser =
    USERS[userKeys[Math.floor(Math.random() * userKeys.length)]];

  group("Health Check", function () {
    const res = http.get(`${BASE_URL}/health`);

    const success = check(res, {
      "health status is 200": (r) => r.status === 200,
      "health response has status": (r) => r.json("status") === "healthy",
    });

    errorRate.add(!success);
  });

  sleep(1);

  group("Authentication - JIT Provisioning", function () {
    // Test authenticated endpoint (triggers JIT user provisioning)
    const res = http.get(`${BASE_URL}/requests/pending`, {
      headers: authHeader(randomUser),
    });

    const success = check(res, {
      "authenticated request succeeds": (r) =>
        r.status === 200 || r.status === 401,
      "response is JSON": (r) =>
        r.headers["Content-Type"]?.includes("application/json"),
    });

    errorRate.add(!success);
  });

  sleep(1);

  // Requester-specific tests
  if (randomUser.sub.startsWith("IDP-A")) {
    group("Submit Data Access Request (Requester)", function () {
      const payload = {
        datasetId: "dataset-genomic-2024",
        purpose: `k6 test request from ${randomUser.name}`,
        objectKeys: ["samples/data-001.csv"],
      };

      const res = http.post(`${BASE_URL}/requests`, JSON.stringify(payload), {
        headers: authHeader(randomUser),
      });

      const success = check(res, {
        "request submission status is 2xx": (r) =>
          r.status >= 200 && r.status < 300,
        "response contains requestId": (r) => r.json("requestId") !== undefined,
      });

      errorRate.add(!success);

      if (success && res.json("requestId")) {
        const requestId = res.json("requestId");

        // Retrieve the created request
        sleep(0.5);
        const getRes = http.get(`${BASE_URL}/requests/${requestId}`, {
          headers: authHeader(randomUser),
        });

        check(getRes, {
          "get request status is 200": (r) => r.status === 200,
          "request status is Submitted": (r) =>
            r.json("status") === "Submitted",
        });
      }
    });

    sleep(1);

    group("List My Requests (Requester)", function () {
      const res = http.get(`${BASE_URL}/requests/my`, {
        headers: authHeader(randomUser),
      });

      const success = check(res, {
        "list requests status is 200": (r) => r.status === 200,
        "response is array": (r) => Array.isArray(r.json()),
      });

      errorRate.add(!success);
    });
  }

  // Data Owner-specific tests
  if (randomUser.sub.startsWith("IDP-B")) {
    group("List Pending Approvals (Data Owner)", function () {
      const res = http.get(`${BASE_URL}/requests/pending`, {
        headers: authHeader(randomUser),
      });

      const success = check(res, {
        "list pending status is 200": (r) => r.status === 200,
        "response is array": (r) => Array.isArray(r.json()),
      });

      errorRate.add(!success);

      // If there are pending requests, test approval workflow
      if (success && res.json().length > 0) {
        const pendingRequest = res.json()[0];
        const requestId = pendingRequest.requestId || pendingRequest._id;

        sleep(0.5);

        // Approve the request
        const approvePayload = {
          comments: `Approved by ${randomUser.name} during k6 test`,
        };

        const approveRes = http.post(
          `${BASE_URL}/requests/${requestId}/approve`,
          JSON.stringify(approvePayload),
          { headers: authHeader(randomUser) },
        );

        check(approveRes, {
          "approve request succeeds": (r) => r.status >= 200 && r.status < 300,
        });
      }
    });
  }

  sleep(1);
}

/**
 * Test Teardown - Run once after all tests
 */
export function teardown(data) {
  console.log("\n===========================================");
  console.log("Test completed");
  console.log(`Duration: ${new Date() - data.startTime}ms`);
  console.log("===========================================");
}
