---
type: Task
task_id: TASK-025
title: "Implement OTP Generation and Validation Logic"
owner: STK-001
status: "In Progress"
related_milestone: MS-008
---

## Description

Implement the `OtpRecord` DocumentDB model, `ISesService` / `SesService` for email sending, and `IOtpService` / `OtpService` for OTP generation and validation in the .NET 10 backend. The service follows the exact same pattern as the existing `IS3Service` / `IStepFunctionsService` pair. Storage reuses DocumentDB (no new infrastructure). OTP codes are 6 digits, expire after 10 minutes, and allow a maximum of 3 validation attempts before being locked.

## Implementation Notes

### 1. `OtpRecord` model

Create `implementation/backend/Models/OtpRecord.cs`:

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZeroTrust.Backend.Models;

[BsonIgnoreExtraElements]
public class OtpRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [BsonElement("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest of the 6-digit plaintext code. Never store plaintext.</summary>
    [BsonElement("code_hash")]
    public string CodeHash { get; set; } = string.Empty;

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Incremented on each failed validation attempt. Locked when >= 3.</summary>
    [BsonElement("attempt_count")]
    public int AttemptCount { get; set; } = 0;

    /// <summary>Set to true after a successful validation. Prevents replay.</summary>
    [BsonElement("used")]
    public bool Used { get; set; } = false;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2. `ISesService` interface

Create `implementation/backend/Services/ISesService.cs`:

```csharp
namespace ZeroTrust.Backend.Services;

public interface ISesService
{
    /// <summary>Send a 6-digit OTP code to the given email address.</summary>
    Task SendOtpEmailAsync(string recipientEmail, string requestId, string otpCode);

    /// <summary>Send an approval outcome notification (approved or rejected).</summary>
    Task SendApprovalOutcomeEmailAsync(string recipientEmail, string requestId, bool approved, string? comments);
}
```

### 3. `SesService` implementation

Create `implementation/backend/Services/SesService.cs`:

```csharp
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using ZeroTrust.Backend.Templates;

namespace ZeroTrust.Backend.Services;

public class SesService : ISesService
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly ILogger<SesService> _logger;
    private readonly string _fromAddress;
    private readonly string? _configurationSet;
    private readonly bool _enabled;

    public SesService(
        IAmazonSimpleEmailServiceV2 sesClient,
        ILogger<SesService> logger,
        IConfiguration configuration)
    {
        _sesClient = sesClient;
        _logger = logger;
        _fromAddress = configuration["AWS:SES:FromAddress"]
            ?? throw new InvalidOperationException("AWS:SES:FromAddress is not configured");
        _configurationSet = configuration["AWS:SES:ConfigurationSet"];
        _enabled = configuration.GetValue<bool>("AWS:SES:Enabled", false);
    }

    public async Task SendOtpEmailAsync(string recipientEmail, string requestId, string otpCode)
    {
        if (!_enabled)
        {
            _logger.LogWarning("SES disabled — OTP for {RequestId} would have been sent to {Email}: {Code}",
                requestId, recipientEmail, otpCode);
            return;
        }

        var (html, text) = EmailTemplates.OtpEmail(requestId, otpCode);
        await SendAsync(
            to: recipientEmail,
            subject: $"Your data access approval code — {requestId}",
            htmlBody: html,
            textBody: text);

        _logger.LogInformation("OTP email sent to {Email} for request {RequestId}", recipientEmail, requestId);
    }

    public async Task SendApprovalOutcomeEmailAsync(
        string recipientEmail, string requestId, bool approved, string? comments)
    {
        if (!_enabled)
        {
            _logger.LogWarning("SES disabled — outcome email for {RequestId} skipped", requestId);
            return;
        }

        var outcome = approved ? "Approved" : "Rejected";
        var (html, text) = EmailTemplates.ApprovalOutcomeEmail(requestId, outcome, comments);
        await SendAsync(
            to: recipientEmail,
            subject: $"Data access request {requestId} has been {outcome}",
            htmlBody: html,
            textBody: text);

        _logger.LogInformation("Outcome email ({Outcome}) sent to {Email} for request {RequestId}",
            outcome, recipientEmail, requestId);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, string textBody)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = _fromAddress,
            Destination = new Destination { ToAddresses = [to] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Html = new Content { Data = htmlBody, Charset = "UTF-8" },
                        Text = new Content { Data = textBody, Charset = "UTF-8" }
                    }
                }
            },
            ConfigurationSetName = _configurationSet
        };

        await _sesClient.SendEmailAsync(request);
    }
}
```

**NuGet package**: add `AWSSDK.SimpleEmailV2` to `ZeroTrust.Backend.csproj`.

### 4. `IOtpService` interface and `OtpValidationResult`

Add to `implementation/backend/Services/ISesService.cs` or a dedicated `IOtpService.cs`:

```csharp
namespace ZeroTrust.Backend.Services;

public enum OtpValidationResult
{
    Success,
    InvalidCode,
    Expired,
    MaxAttemptsExceeded,
    AlreadyUsed,
    NotFound
}

public interface IOtpService
{
    /// <summary>
    /// Generates a 6-digit OTP, stores a hashed record in DocumentDB,
    /// and sends the code to the recipient via SES.
    /// </summary>
    Task GenerateAndSendOtpAsync(string requestId, string recipientEmail);

    /// <summary>
    /// Validates the submitted code against the latest OTP record for the request.
    /// Returns Success and marks the record Used=true on match;
    /// increments AttemptCount and returns a specific failure reason otherwise.
    /// </summary>
    Task<OtpValidationResult> ValidateOtpAsync(string requestId, string code);
}
```

### 5. `OtpService` implementation

Create `implementation/backend/Services/OtpService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public class OtpService : IOtpService
{
    private const int OtpExpiryMinutes = 10;
    private const int MaxAttempts = 3;

    private readonly IDataService _dataService;
    private readonly ISesService _sesService;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IDataService dataService, ISesService sesService, ILogger<OtpService> logger)
    {
        _dataService = dataService;
        _sesService = sesService;
        _logger = logger;
    }

    public async Task GenerateAndSendOtpAsync(string requestId, string recipientEmail)
    {
        // Cryptographically random 6-digit code, zero-padded (e.g. "007423")
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);

        var record = new OtpRecord
        {
            RequestId      = requestId,
            RecipientEmail = recipientEmail,
            CodeHash       = codeHash,
            ExpiresAt      = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
            CreatedAt      = DateTime.UtcNow
        };

        await _dataService.CreateOtpRecordAsync(record);

        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType   = "OTP_SENT",
            RequestId   = requestId,
            Description = $"OTP sent to {recipientEmail}",
            Result      = AuditEventResult.Success
        });

        await _sesService.SendOtpEmailAsync(recipientEmail, requestId, code);

        _logger.LogInformation("OTP generated and sent for request {RequestId}", requestId);
    }

    public async Task<OtpValidationResult> ValidateOtpAsync(string requestId, string code)
    {
        var record = await _dataService.GetLatestOtpRecordAsync(requestId);

        if (record is null)
        {
            _logger.LogWarning("No OTP record found for request {RequestId}", requestId);
            return OtpValidationResult.NotFound;
        }

        if (record.Used)
        {
            _logger.LogWarning("OTP already used for request {RequestId}", requestId);
            return OtpValidationResult.AlreadyUsed;
        }

        if (DateTime.UtcNow > record.ExpiresAt)
        {
            _logger.LogWarning("OTP expired for request {RequestId}", requestId);
            await LogFailedAttemptAsync(requestId, "OTP_FAILED", "Code expired");
            return OtpValidationResult.Expired;
        }

        if (record.AttemptCount >= MaxAttempts)
        {
            _logger.LogWarning("Max OTP attempts exceeded for request {RequestId}", requestId);
            return OtpValidationResult.MaxAttemptsExceeded;
        }

        if (!string.Equals(HashCode(code), record.CodeHash, StringComparison.Ordinal))
        {
            record.AttemptCount++;
            await _dataService.UpdateOtpRecordAsync(record);
            await LogFailedAttemptAsync(requestId, "OTP_FAILED",
                $"Invalid code (attempt {record.AttemptCount}/{MaxAttempts})");

            return record.AttemptCount >= MaxAttempts
                ? OtpValidationResult.MaxAttemptsExceeded
                : OtpValidationResult.InvalidCode;
        }

        // Success path — mark as used
        record.Used = true;
        await _dataService.UpdateOtpRecordAsync(record);

        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType   = "OTP_VALIDATED",
            RequestId   = requestId,
            Description = "OTP validated successfully",
            Result      = AuditEventResult.Success
        });

        _logger.LogInformation("OTP validated successfully for request {RequestId}", requestId);
        return OtpValidationResult.Success;
    }

    // -------------------------------------------------------------------------

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task LogFailedAttemptAsync(string requestId, string eventType, string description)
    {
        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType   = eventType,
            RequestId   = requestId,
            Description = description,
            Result      = AuditEventResult.Failure
        });
    }
}
```

### 6. DocumentDB data service methods to add

Add the following three method signatures to `IDataService` and implement them in `MongoDataService`. The collection name should be `"otp_records"`.

```csharp
// In IDataService:
Task CreateOtpRecordAsync(OtpRecord record);
Task<OtpRecord?> GetLatestOtpRecordAsync(string requestId);
Task UpdateOtpRecordAsync(OtpRecord record);
```

`GetLatestOtpRecordAsync` should sort by `created_at` descending and return the first result — this handles the case where a user requests a second OTP before the first expires.

```csharp
// MongoDataService implementation example:
public async Task<OtpRecord?> GetLatestOtpRecordAsync(string requestId)
{
    var collection = _database.GetCollection<OtpRecord>("otp_records");
    return await collection
        .Find(r => r.RequestId == requestId && !r.Used)
        .SortByDescending(r => r.CreatedAt)
        .FirstOrDefaultAsync();
}
```

### 7. DI registration in `Program.cs`

Add directly after the existing `builder.Services.AddScoped<IStepFunctionsService, StepFunctionsService>();` line:

```csharp
// SES client
if (!string.IsNullOrEmpty(serviceUrl))
{
    // LocalStack (SES v2 is not well supported in LocalStack free tier;
    // SesService checks AWS:SES:Enabled=false so this client is never called locally)
    var sesConfig = new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Config { ServiceURL = serviceUrl };
    builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
        new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"), sesConfig));
}
else
{
    builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
        new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client());
}

builder.Services.AddScoped<ISesService, SesService>();
builder.Services.AddScoped<IOtpService, OtpService>();
```

### 8. Local development behaviour

When `AWS__SES__Enabled=false` (the default in `appsettings.json` and `docker-compose.yml`), `SesService` logs the OTP code at `Warning` level instead of sending it. This makes it trivial to test the full OTP flow locally without real emails — just tail the Docker logs.

## Definition of Done

- [ ] `OtpRecord` model added to `Models/`
- [ ] `ISesService` / `SesService` implemented (reads `AWS:SES:FromAddress` from config)
- [ ] `IOtpService` / `OtpService` implemented with `GenerateAndSendOtpAsync` and `ValidateOtpAsync`
- [ ] `IDataService` extended with `CreateOtpRecordAsync`, `GetLatestOtpRecordAsync`, `UpdateOtpRecordAsync`; `MongoDataService` implements all three
- [ ] `AWSSDK.SimpleEmailV2` NuGet package added to `ZeroTrust.Backend.csproj`
- [ ] `ISesService`, `IOtpService` registered in `Program.cs` DI container
- [ ] `AWS:SES:Enabled=false` set in `appsettings.json` (local dev logs OTP to console instead of sending)
- [ ] Unit tests written for `OtpService`: generate happy path, validate correct code, validate expired, validate max attempts, validate replay (TASK-028)
