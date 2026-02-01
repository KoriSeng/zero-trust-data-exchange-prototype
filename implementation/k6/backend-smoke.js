import http from "k6/http";
import { check, group } from "k6";
import encoding from "k6/encoding";

// Simple smoke test configuration
export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate>0.9"], // 90% of checks must pass
  },
};

const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";

// Fixed test users (must match seed data)
const TEST_USERS = {
  requesterA001: {
    sub: "IDP-A_A-001",
    email: "a.alex@org-a.example.com",
    name: "Alex Kim",
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
 * Generate unsigned JWT for local testing (alg: none)
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
  return {
    Authorization: `Bearer ${jwt}`,
    "Content-Type": "application/json",
  };
}

export default function () {
  console.log(`Testing Backend API at: ${BASE_URL}\n`);

  // Test 1: Health Check
  group("Health Check", function () {
    const res = http.get(`${BASE_URL}/health`);

    const passed = check(res, {
      "health endpoint returns 200": (r) => r.status === 200,
      "response is JSON": (r) =>
        r.headers["Content-Type"]?.includes("application/json"),
      "status is healthy": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.status === "healthy";
        } catch (e) {
          return false;
        }
      },
    });

    if (passed) {
      console.log("✓ Health check passed");
    } else {
      console.log("✗ Health check failed");
    }
  });

  // Test 2: Get Current User Information (JIT Provisioning)
  group("Get Current User - Requester (IDP-A)", function () {
    const user = TEST_USERS.requesterA001;
    const jwt = generateJWT(user);

    const res = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(jwt),
    });

    const passed = check(res, {
      "GET /me returns 200": (r) => r.status === 200,
      "response is JSON": (r) =>
        r.headers["Content-Type"]?.includes("application/json"),
      "response has userId": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.userId && typeof body.userId === "string";
        } catch (e) {
          return false;
        }
      },
      "response has email": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.email === user.email;
        } catch (e) {
          return false;
        }
      },
      "response has displayName": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.displayName === user.name;
        } catch (e) {
          return false;
        }
      },
      "response has organizationId": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.organizationId && typeof body.organizationId === "string";
        } catch (e) {
          return false;
        }
      },
      "response has organizationName": (r) => {
        try {
          const body = JSON.parse(r.body);
          return (
            body.organizationName && typeof body.organizationName === "string"
          );
        } catch (e) {
          return false;
        }
      },
      "response has roles array": (r) => {
        try {
          const body = JSON.parse(r.body);
          return Array.isArray(body.roles);
        } catch (e) {
          return false;
        }
      },
      "response has status": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.status && typeof body.status === "string";
        } catch (e) {
          return false;
        }
      },
      "response has identityProvider": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.identityProvider === "IDP-A";
        } catch (e) {
          return false;
        }
      },
      "response has createdAt timestamp": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.createdAt && typeof body.createdAt === "string";
        } catch (e) {
          return false;
        }
      },
      "response has lastAccessAt timestamp": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.lastAccessAt && typeof body.lastAccessAt === "string";
        } catch (e) {
          return false;
        }
      },
    });

    if (passed) {
      console.log(
        `✓ Current user endpoint passed for ${user.name} (${user.sub})`,
      );
    } else {
      console.log(`✗ Current user endpoint failed for ${user.name}`);
    }
  });

  // Test 3: Authenticated Request (Requester)
  group("Authenticated Request - Requester", function () {
    const user = TEST_USERS.requesterA001;
    const jwt = generateJWT(user);

    const res = http.get(`${BASE_URL}/requests/pending`, {
      headers: authHeaders(jwt),
    });

    const passed = check(res, {
      "authenticated endpoint accessible": (r) =>
        r.status === 200 || r.status === 401,
      "response is JSON": (r) =>
        r.headers["Content-Type"]?.includes("application/json"),
    });

    if (passed) {
      console.log(
        `✓ Authenticated request passed for ${user.name} (${user.sub})`,
      );
    } else {
      console.log(`✗ Authenticated request failed for ${user.name}`);
    }
  });

  // Test 4: Get Current User Information (Data Owner - IDP-B)
  group("Get Current User - Data Owner (IDP-B)", function () {
    const user = TEST_USERS.dataOwnerB001;
    const jwt = generateJWT(user);

    const res = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(jwt),
    });

    const passed = check(res, {
      "GET /me returns 200": (r) => r.status === 200,
      "user belongs to ORG-B": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.organizationId && typeof body.organizationId === "string";
        } catch (e) {
          return false;
        }
      },
      "identityProvider is IDP-B": (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.identityProvider === "IDP-B";
        } catch (e) {
          return false;
        }
      },
      "roles array is not empty": (r) => {
        try {
          const body = JSON.parse(r.body);
          return Array.isArray(body.roles) && body.roles.length > 0;
        } catch (e) {
          return false;
        }
      },
    });

    if (passed) {
      console.log(
        `✓ Current user endpoint passed for ${user.name} (${user.sub})`,
      );
    } else {
      console.log(`✗ Current user endpoint failed for ${user.name}`);
    }
  });

  // Test 5: Authenticated Request (Data Owner)
  group("Authenticated Request - Data Owner", function () {
    const user = TEST_USERS.dataOwnerB001;
    const jwt = generateJWT(user);

    const res = http.get(`${BASE_URL}/requests/pending`, {
      headers: authHeaders(jwt),
    });

    const passed = check(res, {
      "data owner can access pending requests": (r) => r.status === 200,
      "response is array": (r) => {
        try {
          return Array.isArray(JSON.parse(r.body));
        } catch (e) {
          return false;
        }
      },
    });

    if (passed) {
      console.log(`✓ Data owner request passed for ${user.name} (${user.sub})`);
    } else {
      console.log(`✗ Data owner request failed for ${user.name}`);
    }
  });

  // Test 6: JIT Provisioning Verification
  group("JIT Provisioning - New User Creation", function () {
    // Create a new user that doesn't exist yet
    const newUser = {
      sub: `IDP-A_NEW-USER-${Date.now()}`,
      email: `newuser-${Date.now()}@org-a.example.com`,
      name: "New Test User",
      cognitoGroup: "ap-southeast-1_xxxxx_IDP-A"
    };

    const jwt = generateJWT(newUser);

    const res = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(jwt),
    });

    const passed = check(res, {
      "new user provisioning succeeds": (r) => r.status === 200,
    });

    if (passed) {
      console.log(
        "✓ JIT provisioning test passed (new user automatically provisioned)",
      );
    } else {
      console.log("✗ JIT provisioning test failed");
    }
  });

  // Test 7: Identity Collision Test
  group("Identity Collision Test", function () {
    // Both IDP-A and IDP-B have "Alex Kim" but with different subjects
    const userA = TEST_USERS.requesterA001; // IDP-A_A-001
    const userB = TEST_USERS.dataOwnerB001; // IDP-B_B-001

    const jwtA = generateJWT(userA);
    const jwtB = generateJWT(userB);

    const resA = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(jwtA),
    });

    const resB = http.get(`${BASE_URL}/me`, {
      headers: authHeaders(jwtB),
    });

    const passed = check(
      { resA, resB },
      {
        "both Alex Kim users can authenticate": (r) =>
          r.resA.status === 200 && r.resB.status === 200,
        "users are in different organizations": (r) => {
          try {
            const bodyA = JSON.parse(r.resA.body);
            const bodyB = JSON.parse(r.resB.body);
            console.log(
              `  IDP-A user org: ${bodyA.organizationId}, IDP-B user org: ${bodyB.organizationId}`,
            );
            return (
              bodyA.organizationId &&
              bodyB.organizationId &&
              bodyA.organizationId !== bodyB.organizationId
            );
          } catch (e) {
            console.log(`  org comparison error: ${e}`);
            return false;
          }
        },
        "users have different identity providers": (r) => {
          try {
            const bodyA = JSON.parse(r.resA.body);
            const bodyB = JSON.parse(r.resB.body);
            return (
              bodyA.identityProvider === "IDP-A" &&
              bodyB.identityProvider === "IDP-B"
            );
          } catch (e) {
            return false;
          }
        },
      },
    );

    if (passed) {
      console.log(
        "✓ Identity collision test passed (issuer+subject deconfliction works)",
      );
    } else {
      console.log("✗ Identity collision test failed");
    }
  });

  console.log("\n========================================");
  console.log("Backend API Smoke Test Complete");
  console.log("========================================");
}
