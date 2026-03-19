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

public sealed class OtpDispatchPreview
{
    public string OtpCode { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string TextBody { get; init; } = string.Empty;
}

public interface IOtpService
{
    Task<OtpDispatchPreview> GenerateAndSendOtpAsync(string requestId, string recipientEmail);
    Task<OtpValidationResult> ValidateOtpAsync(string requestId, string code);
}
