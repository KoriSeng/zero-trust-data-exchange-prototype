import http from "k6/http";
import { check, group, sleep } from "k6";
import encoding from "k6/encoding";

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate==1.0"],
  },
};

const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";
const KNOWN_OTP = __ENV.KNOWN_OTP_CODE || null;
const REPLAY_REQUEST_ID = __ENV.REPLAY_REQUEST_ID || null;

const DATA_OWNER = {
  sub: "IDP-B_B-001",
  email: "b.alex@org-b.example.com",
  name: "Alex Kim",
  cognitoGroup: "ap-southeast-1_yyyyy_IDP-B",
};

const REQUESTER = {
  sub: "IDP-A_A-001",
  email: "a.alex@org-a.example.com",
  name: "Alex Kim",
  cognitoGroup: "ap-southeast-1_xxxxx_IDP-A",
};

function generateJwt(user) {
  const header = { alg: "none", typ: "JWT" };
  const now = Math.floor(Date.now() / 1000);
  const payload = {
    sub: user.sub,
    email: user.email,
    name: user.name,
    "cognito:groups": [user.cognitoGroup],
    iat: now,
    exp: now + 3600,
  };

  const encode = (obj) => encoding.b64encode(JSON.stringify(obj), "rawurl");
  return `${encode(header)}.${encode(payload)}.`;
}

function requesterHeaders() {
  return {
    Authorization: `Bearer ${generateJwt(REQUESTER)}`,
    "Content-Type": "application/json",
  };
}

function ownerHeaders() {
  return {
    Authorization: `Bearer ${generateJwt(DATA_OWNER)}`,
    "Content-Type": "application/json",
  };
}

function submitRequest() {
  const requestPayload = {
    datasetId: "dataset-genomic-2024",
    datasetName: "Genomic Research Dataset 2024",
    objectKeys: ["samples/data-001.csv"],
    purpose: "OTP security test",
    dataOwnerOrg: "ORG-B",
  };

  const response = http.post(`${BASE_URL}/requests`, JSON.stringify(requestPayload), {
    headers: requesterHeaders(),
  });

  check(response, { "request created (201)": (r) => r.status === 201 });
  const body = response.json();
  return body?.id;
}

export default function () {
  group("no-otp-sent: approve without OTP should return 400", () => {
    const requestId = submitRequest();
    sleep(0.4);

    const response = http.post(
      `${BASE_URL}/requests/${requestId}/approve`,
      JSON.stringify({ approvalCode: "000000", comments: "test" }),
      { headers: ownerHeaders() },
    );

    check(response, {
      "status is 400": (r) => r.status === 400,
      "body contains no OTP found message": (r) =>
        r.body.includes("No OTP found") || r.body.includes("Request a code first"),
    });
  });

  group("otp-send: POST /otp/send returns 200", () => {
    const requestId = submitRequest();
    sleep(0.4);

    const response = http.post(
      `${BASE_URL}/requests/${requestId}/otp/send`,
      null,
      { headers: ownerHeaders() },
    );

    check(response, {
      "otp send returns 200": (r) => r.status === 200,
    });
  });

  group("invalid-code: wrong OTP returns 400", () => {
    const requestId = submitRequest();
    sleep(0.4);

    http.post(`${BASE_URL}/requests/${requestId}/otp/send`, null, {
      headers: ownerHeaders(),
    });
    sleep(0.4);

    const response = http.post(
      `${BASE_URL}/requests/${requestId}/approve`,
      JSON.stringify({ approvalCode: "123123", comments: "wrong otp" }),
      { headers: ownerHeaders() },
    );

    check(response, {
      "wrong code returns 400": (r) => r.status === 400,
    });
  });

  group("brute-force: 3 wrong codes then locked out with 429", () => {
    const requestId = submitRequest();
    sleep(0.4);

    http.post(`${BASE_URL}/requests/${requestId}/otp/send`, null, {
      headers: ownerHeaders(),
    });
    sleep(0.4);

    for (let attempt = 1; attempt <= 2; attempt++) {
      const response = http.post(
        `${BASE_URL}/requests/${requestId}/approve`,
        JSON.stringify({ approvalCode: `10000${attempt}`, comments: "bad attempt" }),
        { headers: ownerHeaders() },
      );

      check(response, {
        [`attempt ${attempt} returns 400`]: (r) => r.status === 400,
      });
    }

    const third = http.post(
      `${BASE_URL}/requests/${requestId}/approve`,
      JSON.stringify({ approvalCode: "100003", comments: "bad attempt 3" }),
      { headers: ownerHeaders() },
    );

    check(third, {
      "third wrong attempt returns 429": (r) => r.status === 429,
    });

    const fourth = http.post(
      `${BASE_URL}/requests/${requestId}/approve`,
      JSON.stringify({ approvalCode: "100004", comments: "bad attempt 4" }),
      { headers: ownerHeaders() },
    );

    check(fourth, {
      "fourth attempt remains 429": (r) => r.status === 429,
    });
  });

  group("replay: valid code accepted once, rejected on reuse", () => {
    if (!KNOWN_OTP || !REPLAY_REQUEST_ID) {
      console.warn(
        "Replay test skipped. Provide REPLAY_REQUEST_ID and KNOWN_OTP_CODE to run this scenario.",
      );
      return;
    }

    const first = http.post(
      `${BASE_URL}/requests/${REPLAY_REQUEST_ID}/approve`,
      JSON.stringify({ approvalCode: KNOWN_OTP, comments: "first use" }),
      { headers: ownerHeaders() },
    );

    const second = http.post(
      `${BASE_URL}/requests/${REPLAY_REQUEST_ID}/approve`,
      JSON.stringify({ approvalCode: KNOWN_OTP, comments: "replay attempt" }),
      { headers: ownerHeaders() },
    );

    check(first, {
      "first approval succeeds": (r) => r.status === 200,
    });

    check(second, {
      "replay is rejected": (r) => r.status === 400,
      "replay mentions already used": (r) =>
        r.body.includes("already been used") || r.body.includes("AlreadyUsed"),
    });
  });
}
