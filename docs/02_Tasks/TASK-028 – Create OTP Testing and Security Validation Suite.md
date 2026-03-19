---
type: Task
task_id: TASK-028
title: "Create OTP Testing and Security Validation Suite"
owner: STK-001
status: "In Progress"
related_milestone: MS-008
---

## Description

Write the OTP security test suite: a k6 script that exercises API-level OTP security properties (invalid codes, replay, brute force lockout), and C# unit tests for `OtpService` covering the core validation logic (expiry, attempt locking, replay prevention). The k6 tests do **not** require real email delivery — they rely on the backend returning deterministic error responses.

## Implementation Notes

### 1. k6 test file: `implementation/k6/otp-security.js`

Create the file with the following structure. The script targets a running backend instance (`BACKEND_API_URL`) with seed data matching the existing smoke tests:

```javascript
/**
 * OTP Security Test Suite
 *
 * Tests OTP API security properties without real email delivery.
 * The backend must be running with AWS:SES:Enabled=false (local dev mode).
 * In this mode, the OTP code is logged to stdout — extract it from Docker logs
 * for the valid-flow test, or test only the error-response paths below.
 *
 * Run:
 *   k6 run implementation/k6/otp-security.js \
 *       -e BACKEND_API_URL=http://localhost:5000 \
 *       -e KNOWN_OTP_CODE=123456          # optional: retrieved from Docker logs
 *
 * Test coverage:
 *   1. Send OTP — POST /requests/{id}/otp/send returns 200
 *   2. Invalid code — POST /requests/{id}/approve with wrong code returns 400
 *   3. Brute force lockout — 3 wrong codes → 4th attempt returns 429
 *   4. OTP replay — valid code used twice → second use returns 400
 *   5. No OTP — approve without sending first returns 400 "No OTP found"
 */

import http from "k6/http";
import { check, group, sleep } from "k6";
import encoding from "k6/encoding";

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate==1.0"], // all security checks must pass
  },
};

const BASE_URL = __ENV.BACKEND_API_URL || "http://localhost:5000";
const KNOWN_OTP = __ENV.KNOWN_OTP_CODE || null; // set from Docker log extraction

// ---------------------------------------------------------------------------
// Test users — must match DatabaseSeederHostedService seed data
// ---------------------------------------------------------------------------
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

function generateJWT(user) {
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
  const encode = (obj) =>
    encoding.b64encode(JSON.stringify(obj), "rawurl");
  return `${encode(header)}.${encode(payload)}.`;
}

function ownerHeaders() {
  return {
    Authorization: `Bearer ${generateJWT(DATA_OWNER)}`,
    "Content-Type": "application/json",
  };
}

function requesterHeaders() {
  return {
    Authorization: `Bearer ${generateJWT(REQUESTER)}`,
    "Content-Type": "application/json",
  };
}

// ---------------------------------------------------------------------------
// Helper: submit a fresh data access request and return its ID
// ---------------------------------------------------------------------------
function submitRequest() {
  const body = JSON.stringify({
    datasetIds: ["dataset-001"],
    purpose: "OTP security test",
    requestedDurationDays: 7,
  });
  const res = http.post(`${BASE_URL}/requests`, body, {
    headers: requesterHeaders(),
  });
  check(res, { "request created (201)": (r) => r.status === 201 });
  return JSON.parse(res.body).id;
}

// ---------------------------------------------------------------------------
// Test scenarios
// ---------------------------------------------------------------------------
export default function () {

  // ── Scenario 1: Approve without sending OTP first ─────────────────────────
  group("no-otp-sent: approve without OTP should return 400", () => {
    const rid = submitRequest();
    sleep(0.5);

    const res = http.post(
      `${BASE_URL}/requests/${rid}/approve`,
      JSON.stringify({ approvalCode: "000000", comments: "test" }),
      { headers: ownerHeaders() }
    );

    check(res, {
      "status is 400": (r) => r.status === 400,
      "body contains 'No OTP found'": (r) =>
        r.body.includes("No OTP found") || r.body.includes("No OTP"),
    });
  });

  // ── Scenario 2: Send OTP — endpoint returns 200 ───────────────────────────
  group("otp-send: POST /otp/send returns 200", () => {
    const rid = submitRequest();
    sleep(0.5);

    const res = http.post(
      `${BASE_URL}/requests/${rid}/otp/send`,
      null,
      { headers: ownerHeaders() }
    );

    check(res, {
      "otp/send status 200": (r) => r.status === 200,
    });
  });

  // ── Scenario 3: Invalid code returns 400 ─────────────────────────────────
  group("invalid-code: wrong OTP returns 400", () => {
    const rid = submitRequest();
    sleep(0.5);

    http.post(`${BASE_URL}/requests/${rid}/otp/send`, null, {
      headers: ownerHeaders(),
    });
    sleep(0.5);

    const res = http.post(
      `${BASE_URL}/requests/${rid}/approve`,
      JSON.stringify({ approvalCode: "000000", comments: "" }),
      { headers: ownerHeaders() }
    );

    check(res, {
      "wrong code → 400": (r) => r.status === 400,
    });
  });

  // ── Scenario 4: Brute-force lockout after 3 wrong attempts ────────────────
  group("brute-force: 3 wrong codes → 429 on 3rd attempt", () => {
    const rid = submitRequest();
    sleep(0.5);

    http.post(`${BASE_URL}/requests/${rid}/otp/send`, null, {
      headers: ownerHeaders(),
    });
    sleep(0.5);

    let lastStatus;
    for (let attempt = 1; attempt <= 3; attempt++) {
      const res = http.post(
        `${BASE_URL}/requests/${rid}/approve`,
        JSON.stringify({ approvalCode: `00000${attempt}`, comments: "" }),
        { headers: ownerHeaders() }
      );
      lastStatus = res.status;

      if (attempt < 3) {
        check(res, {
          [`attempt ${attempt}: 400 invalid code`]: (r) =>
            r.status === 400,
        });
      }
    }

    // After MaxAttempts (3) wrong codes the response must be 429
    check({ status: lastStatus }, {
      "3rd wrong attempt → 429": (s) => s.status === 429,
    });

    // Attempt 4 — still locked even if we somehow send a different code
    const res4 = http.post(
      `${BASE_URL}/requests/${rid}/approve`,
      JSON.stringify({ approvalCode: "999999", comments: "" }),
      { headers: ownerHeaders() }
    );
    check(res4, {
      "4th attempt still → 429": (r) => r.status === 429,
    });
  });

  // ── Scenario 5: OTP replay — valid code rejected on second use ────────────
  // Requires KNOWN_OTP_CODE env var (retrieved from Docker logs after otp/send)
  group("replay: valid code accepted once, rejected on reuse", () => {
    if (!KNOWN_OTP) {
      console.warn(
        "KNOWN_OTP_CODE not set — skipping replay test. " +
        "Extract the code from Docker logs after POST /otp/send and re-run with " +
        "-e KNOWN_OTP_CODE=<code>"
      );
      return;
    }

    const rid = submitRequest();
    sleep(0.5);

    http.post(`${BASE_URL}/requests/${rid}/otp/send`, null, {
      headers: ownerHeaders(),
    });
    sleep(0.5);

    // First use — should succeed
    const res1 = http.post(
      `${BASE_URL}/requests/${rid}/approve`,
      JSON.stringify({ approvalCode: KNOWN_OTP, comments: "first use" }),
      { headers: ownerHeaders() }
    );
    check(res1, {
      "first use → 200 approved": (r) => r.status === 200,
    });

    // Second use — must be rejected
    const res2 = http.post(
      `${BASE_URL}/requests/${rid}/approve`,
      JSON.stringify({ approvalCode: KNOWN_OTP, comments: "replay attempt" }),
      { headers: ownerHeaders() }
    );
    check(res2, {
      "replay → 400 already used": (r) => r.status === 400,
      "replay body mentions 'already been used'": (r) =>
        r.body.includes("already been used") || r.body.includes("AlreadyUsed"),
    });
  });
}
```

### 2. C# unit tests for `OtpService`

Create `implementation/backend.Tests/OtpServiceTests.cs` (or add to an existing test project). Use `xUnit` and `NSubstitute` (or `Moq`) to mock `IDataService` and `ISesService`:

```csharp
using System.Security.Cryptography;
using System.Text;
using NSubstitute;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;
using ZeroTrust.Backend.Services;
using Xunit;

namespace ZeroTrust.Backend.Tests;

public class OtpServiceTests
{
    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static OtpRecord ValidRecord(string requestId, string code) => new()
    {
        RequestId  = requestId,
        CodeHash   = HashCode(code),
        ExpiresAt  = DateTime.UtcNow.AddMinutes(10),
        Used       = false,
        AttemptCount = 0
    };

    // ── GenerateAndSendOtpAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GenerateAndSendOtp_CreatesRecordAndSendsEmail()
    {
        var dataService = Substitute.For<IDataService>();
        var sesService  = Substitute.For<ISesService>();
        var logger      = Substitute.For<ILogger<OtpService>>();
        var svc = new OtpService(dataService, sesService, logger);

        await svc.GenerateAndSendOtpAsync("req-001", "owner@example.com");

        await dataService.Received(1).CreateOtpRecordAsync(
            Arg.Is<OtpRecord>(r =>
                r.RequestId == "req-001" &&
                r.RecipientEmail == "owner@example.com" &&
                !string.IsNullOrEmpty(r.CodeHash) &&
                r.ExpiresAt > DateTime.UtcNow));

        await sesService.Received(1).SendOtpEmailAsync(
            "owner@example.com",
            "req-001",
            Arg.Is<string>(c => c.Length == 6 && c.All(char.IsDigit)));
    }

    // ── ValidateOtpAsync — success ───────────────────────────────────────────

    [Fact]
    public async Task ValidateOtp_CorrectCode_ReturnsSuccess()
    {
        const string code = "482931";
        var record = ValidRecord("req-002", code);

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-002").Returns(record);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-002", code);

        Assert.Equal(OtpValidationResult.Success, result);
        Assert.True(record.Used);
        await dataService.Received(1).UpdateOtpRecordAsync(
            Arg.Is<OtpRecord>(r => r.Used == true));
    }

    // ── ValidateOtpAsync — expired ───────────────────────────────────────────

    [Fact]
    public async Task ValidateOtp_ExpiredRecord_ReturnsExpired()
    {
        const string code = "111111";
        var record = ValidRecord("req-003", code);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // already expired

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-003").Returns(record);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-003", code);

        Assert.Equal(OtpValidationResult.Expired, result);
        Assert.False(record.Used); // must NOT be marked used
    }

    // ── ValidateOtpAsync — max attempts ─────────────────────────────────────

    [Fact]
    public async Task ValidateOtp_AttemptCountAtMax_ReturnsMaxAttemptsExceeded()
    {
        const string code = "222222";
        var record = ValidRecord("req-004", code);
        record.AttemptCount = 3; // already at the limit

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-004").Returns(record);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-004", "999999");

        Assert.Equal(OtpValidationResult.MaxAttemptsExceeded, result);
        Assert.Equal(3, record.AttemptCount); // must NOT increment further
    }

    // ── ValidateOtpAsync — replay ────────────────────────────────────────────

    [Fact]
    public async Task ValidateOtp_AlreadyUsedRecord_ReturnsAlreadyUsed()
    {
        const string code = "333333";
        var record = ValidRecord("req-005", code);
        record.Used = true; // already consumed

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-005").Returns(record);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-005", code);

        Assert.Equal(OtpValidationResult.AlreadyUsed, result);
    }

    // ── ValidateOtpAsync — not found ─────────────────────────────────────────

    [Fact]
    public async Task ValidateOtp_NoRecord_ReturnsNotFound()
    {
        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-999").Returns((OtpRecord?)null);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-999", "000000");

        Assert.Equal(OtpValidationResult.NotFound, result);
    }

    // ── ValidateOtpAsync — wrong code increments attempt count ───────────────

    [Fact]
    public async Task ValidateOtp_WrongCode_IncrementsAttemptCount()
    {
        const string code = "444444";
        var record = ValidRecord("req-006", code);

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-006").Returns(record);

        var svc = new OtpService(dataService, Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await svc.ValidateOtpAsync("req-006", "000000");

        Assert.Equal(OtpValidationResult.InvalidCode, result);
        Assert.Equal(1, record.AttemptCount);
        await dataService.Received(1).UpdateOtpRecordAsync(
            Arg.Is<OtpRecord>(r => r.AttemptCount == 1));
    }
}
```

### 3. Running the tests

**k6 (API-level, local dev)**:
```bash
# Start the backend stack
docker-compose up -d

# Extract OTP from logs if needed for replay test:
#   docker-compose logs backend | grep "OTP for" | tail -1

k6 run implementation/k6/otp-security.js \
    -e BACKEND_API_URL=http://localhost:5000

# With known OTP for replay test:
k6 run implementation/k6/otp-security.js \
    -e BACKEND_API_URL=http://localhost:5000 \
    -e KNOWN_OTP_CODE=482931
```

**C# unit tests**:
```bash
cd implementation/backend
dotnet test --filter "FullyQualifiedName~OtpServiceTests" --logger "console;verbosity=normal"
```

### 4. What each test validates (security properties)

| Test | Property |
|------|----------|
| No OTP sent → approve fails | Cannot approve without initiating OTP flow |
| Invalid code → 400 | Wrong code is rejected |
| 3 wrong codes → 429 | Brute force is rate-limited |
| 4th attempt still 429 | Lock persists after max attempts |
| Valid code → 200 | Happy path works end-to-end |
| Replay → 400 | Used OTP cannot be reused |
| `Used=true` unit test | Replay rejected at service layer |
| `AttemptCount=3` unit test | Lock enforced at service layer |
| Expired record unit test | Expiry enforced at service layer |

### 5. Limitations and notes

- **k6 cannot intercept emails**: The replay test requires the OTP to be retrieved out-of-band (Docker log extraction). Document this in the k6 script header. For CI, mock the OTP or extend `IDataService` with a test-only "get last OTP for request" endpoint gated behind a feature flag.
- **No concurrency tests**: Concurrent OTP attempts are a more advanced scenario; out of scope for this POC. Leave a `// TODO` comment noting that a distributed lock (e.g. MongoDB `findOneAndUpdate` with a condition) would be needed in production.

## Definition of Done

- [ ] `implementation/k6/otp-security.js` created with all five scenario groups
- [ ] k6 script runs against local backend with `thresholds: checks rate==1.0` passing (excluding replay test when `KNOWN_OTP_CODE` not set)
- [ ] C# unit tests in `OtpServiceTests.cs` cover: generate happy path, validate success, validate expired, validate max attempts, validate replay, validate wrong code increments attempt count
- [ ] All C# unit tests pass: `dotnet test` exits 0
- [ ] Test file locations documented in `implementation/k6/README.md` (or equivalent)
