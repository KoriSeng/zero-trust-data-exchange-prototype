namespace ZeroTrust.Backend.Models;

/// <summary>
/// Request to create a new data access request
/// </summary>
public class CreateDataAccessRequestDto
{
    /// <summary>
    /// Dataset ID being requested (verified against catalog)
    /// </summary>
    public string DatasetId { get; set; } = null!;

    /// <summary>
    /// Purpose/justification for access
    /// </summary>
    public string Purpose { get; set; } = null!;

    /// <summary>
    /// Optional metadata or additional context
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Response after creating a data access request
/// </summary>
public class DataAccessRequestResponse
{
    /// <summary>
    /// Internal request ID (UUID)
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Human-readable request identifier (e.g., "REQ-0001")
    /// </summary>
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// Current request status
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Timestamp of request creation
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Message or error details
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Request approval payload
/// </summary>
public class ApproveRequestDto
{
    /// <summary>
    /// Legacy approval decision flag (ignored by API logic)
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// One-time approval code sent to the data owner email
    /// </summary>
    public string ApprovalCode { get; set; } = string.Empty;

    /// <summary>
    /// Approval reason/comments
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// Duration for access (in seconds)
    /// </summary>
    public int? AccessDurationSeconds { get; set; }

    /// <summary>
    /// Legacy duration for access (in hours). Kept for backward compatibility.
    /// </summary>
    public int? AccessDurationHours { get; set; }

    /// <summary>
    /// Optional conditions/restrictions
    /// </summary>
    public Dictionary<string, string>? Conditions { get; set; }
}

/// <summary>
/// Request denial payload
/// </summary>
public class DenyRequestDto
{
    /// <summary>
    /// Reason for denial
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// OTP redemption payload
/// </summary>
public class RedeemRequestDto
{
    /// <summary>
    /// Legacy OTP field retained for backward compatibility
    /// </summary>
    public string? Otp { get; set; }
}

