using System.Security.Cryptography;
using System.Text;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;
using ZeroTrust.Backend.Templates;

namespace ZeroTrust.Backend.Services;

public class OtpService : IOtpService
{
    private const int OtpExpiryMinutes = 10;
    private const int MaxAttempts = 3;
    private const int MaxValidationRetries = 5;

    private readonly IDataService _dataService;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IDataService dataService, ILogger<OtpService> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task<OtpDispatchPreview> GenerateAndSendOtpAsync(string requestId, string recipientEmail)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
        var record = new OtpRecord
        {
            RequestId = requestId,
            RecipientEmail = recipientEmail,
            CodeHash = HashCode(code),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0,
            Used = false,
            LockedAt = null
        };

        await _dataService.CreateOtpRecordAsync(record);
        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "OTP_SENT",
            RequestId = requestId,
            Description = $"OTP sent to {recipientEmail}",
            Result = AuditEventResult.Success
        });

        var subject = $"Your data access approval code — {requestId}";
        var (_, textBody) = EmailTemplates.OtpEmail(requestId, code);

        _logger.LogInformation(
            "OTP generated for request {RequestId} in debug-preview mode (no external email delivery)",
            requestId);

        return new OtpDispatchPreview
        {
            OtpCode = code,
            ExpiresAt = expiresAt,
            RecipientEmail = recipientEmail,
            Subject = subject,
            TextBody = textBody
        };
    }

    public async Task<OtpValidationResult> ValidateOtpAsync(string requestId, string code)
    {
        var submittedCode = code?.Trim();
        if (string.IsNullOrWhiteSpace(submittedCode))
        {
            return OtpValidationResult.InvalidCode;
        }

        var submittedHash = HashCode(submittedCode);

        for (var retry = 0; retry < MaxValidationRetries; retry++)
        {
            var now = DateTime.UtcNow;
            var record = await _dataService.GetLatestOtpRecordAsync(requestId);
            if (record is null)
            {
                _logger.LogWarning("No OTP record found for request {RequestId}", requestId);
                return OtpValidationResult.NotFound;
            }

            var terminalResult = GetTerminalValidationResult(record, now);
            if (terminalResult.HasValue)
            {
                if (terminalResult == OtpValidationResult.Expired)
                {
                    await LogFailedAttemptAsync(requestId, "OTP_EXPIRED", "OTP expired before validation");
                }

                return terminalResult.Value;
            }

            if (string.Equals(record.CodeHash, submittedHash, StringComparison.Ordinal))
            {
                var markedUsed = await _dataService.TryMarkOtpUsedAsync(record.Id, record.AttemptCount);
                if (markedUsed)
                {
                    await LogSuccessfulValidationAsync(requestId);
                    return OtpValidationResult.Success;
                }

                _logger.LogWarning(
                    "OTP validation compare-and-set conflict for request {RequestId} on success path (retry {Retry}/{MaxRetries})",
                    requestId,
                    retry + 1,
                    MaxValidationRetries);
                continue;
            }

            var nextAttemptCount = record.AttemptCount + 1;
            DateTime? lockedAt = nextAttemptCount >= MaxAttempts ? now : null;
            var incremented = await _dataService.TryIncrementOtpAttemptAsync(record.Id, record.AttemptCount, lockedAt);
            if (incremented)
            {
                await LogFailedAttemptAsync(
                    requestId,
                    "OTP_INVALID",
                    $"Invalid OTP code (attempt {nextAttemptCount}/{MaxAttempts})");

                return nextAttemptCount >= MaxAttempts
                    ? OtpValidationResult.MaxAttemptsExceeded
                    : OtpValidationResult.InvalidCode;
            }

            _logger.LogWarning(
                "OTP validation compare-and-set conflict for request {RequestId} on failure path (retry {Retry}/{MaxRetries})",
                requestId,
                retry + 1,
                MaxValidationRetries);
        }

        var latestRecord = await _dataService.GetLatestOtpRecordAsync(requestId);
        if (latestRecord is null)
        {
            _logger.LogWarning("No OTP record found for request {RequestId} after validation retries", requestId);
            return OtpValidationResult.NotFound;
        }

        var latestTerminalResult = GetTerminalValidationResult(latestRecord, DateTime.UtcNow);
        if (latestTerminalResult.HasValue)
        {
            if (latestTerminalResult == OtpValidationResult.Expired)
            {
                await LogFailedAttemptAsync(requestId, "OTP_EXPIRED", "OTP expired before validation");
            }

            return latestTerminalResult.Value;
        }

        if (string.Equals(latestRecord.CodeHash, submittedHash, StringComparison.Ordinal) &&
            await _dataService.TryMarkOtpUsedAsync(latestRecord.Id, latestRecord.AttemptCount))
        {
            await LogSuccessfulValidationAsync(requestId);
            return OtpValidationResult.Success;
        }

        return OtpValidationResult.InvalidCode;
    }

    private async Task LogFailedAttemptAsync(string requestId, string eventType, string description)
    {
        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = eventType,
            RequestId = requestId,
            Description = description,
            Result = AuditEventResult.Failure
        });
    }

    private async Task LogSuccessfulValidationAsync(string requestId)
    {
        await _dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "OTP_VALIDATED",
            RequestId = requestId,
            Description = "OTP validated successfully",
            Result = AuditEventResult.Success
        });
    }

    private static OtpValidationResult? GetTerminalValidationResult(OtpRecord record, DateTime nowUtc)
    {
        if (record.Used)
        {
            return OtpValidationResult.AlreadyUsed;
        }

        if (record.AttemptCount >= MaxAttempts || record.LockedAt.HasValue)
        {
            return OtpValidationResult.MaxAttemptsExceeded;
        }

        if (nowUtc > record.ExpiresAt)
        {
            return OtpValidationResult.Expired;
        }

        return null;
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
