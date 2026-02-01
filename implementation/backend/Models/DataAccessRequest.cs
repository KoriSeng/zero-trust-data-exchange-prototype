namespace ZeroTrust.Backend.Models;

using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Data access request with embedded agreement for immutable audit trail
/// </summary>
[BsonIgnoreExtraElements]
public class DataAccessRequest
{
    /// <summary>
    /// Request ID (UUID)
    /// </summary>
    [BsonId]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// Cognito sub of requester
    /// </summary>
    [BsonElement("requester_id")]
    public string RequesterId { get; set; } = null!;

    /// <summary>
    /// Email for OTP delivery
    /// </summary>
    [BsonElement("requester_email")]
    public string RequesterEmail { get; set; } = null!;

    /// <summary>
    /// Organization of requester
    /// </summary>
    [BsonElement("requester_org")]
    public string RequesterOrg { get; set; } = null!;

    /// <summary>
    /// Dataset ID being requested
    /// </summary>
    [BsonElement("dataset_id")]
    public string DatasetId { get; set; } = null!;

    /// <summary>
    /// Human-readable dataset name
    /// </summary>
    [BsonElement("dataset_name")]
    public string? DatasetName { get; set; }

    /// <summary>
    /// S3 object keys requested
    /// </summary>
    [BsonElement("object_keys")]
    public List<string> ObjectKeys { get; set; } = new();

    /// <summary>
    /// Stated purpose/justification for access
    /// </summary>
    [BsonElement("purpose")]
    public string Purpose { get; set; } = null!;

    /// <summary>
    /// Current workflow state
    /// </summary>
    [BsonElement("status")]
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    /// <summary>
    /// Organization that owns the dataset
    /// </summary>
    [BsonElement("data_owner_org")]
    public string DataOwnerOrg { get; set; } = null!;

    /// <summary>
    /// Request submission timestamp
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last state change timestamp
    /// </summary>
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request expiration (TTL: auto-delete after 90 days)
    /// </summary>
    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Cognito sub of approver
    /// </summary>
    [BsonElement("approved_by")]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp
    /// </summary>
    [BsonElement("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Cognito sub of denier
    /// </summary>
    [BsonElement("denied_by")]
    public string? DeniedBy { get; set; }

    /// <summary>
    /// Denial timestamp
    /// </summary>
    [BsonElement("denied_at")]
    public DateTime? DeniedAt { get; set; }

    /// <summary>
    /// Optional reason for denial
    /// </summary>
    [BsonElement("denial_reason")]
    public string? DenialReason { get; set; }

    /// <summary>
    /// Hashed OTP token for redemption
    /// </summary>
    [BsonElement("otp_token")]
    public string? OtpToken { get; set; }

    /// <summary>
    /// OTP expiration (15 minutes from approval)
    /// </summary>
    [BsonElement("otp_expires_at")]
    public DateTime? OtpExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when OTP was redeemed
    /// </summary>
    [BsonElement("redeemed_at")]
    public DateTime? RedeemedAt { get; set; }

    /// <summary>
    /// Pre-signed S3 URL (ephemeral, not stored long-term)
    /// </summary>
    [BsonElement("presigned_url")]
    public string? PresignedUrl { get; set; }

    /// <summary>
    /// Pre-signed URL expiration (15 minutes)
    /// </summary>
    [BsonElement("presigned_url_expires_at")]
    public DateTime? PresignedUrlExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when data was accessed
    /// </summary>
    [BsonElement("accessed_at")]
    public DateTime? AccessedAt { get; set; }

    /// <summary>
    /// Manual revocation timestamp
    /// </summary>
    [BsonElement("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Cognito sub of revoker
    /// </summary>
    [BsonElement("revoked_by")]
    public string? RevokedBy { get; set; }

    /// <summary>
    /// Reference to user agreement
    /// </summary>
    [BsonElement("user_agreement_id")]
    public string? UserAgreementId { get; set; }

    /// <summary>
    /// Version of agreement at request time
    /// </summary>
    [BsonElement("user_agreement_version")]
    public int? UserAgreementVersion { get; set; }

    /// <summary>
    /// FULL AGREEMENT CONTENT EMBEDDED at request time
    /// Ensures request is self-contained with complete legal context
    /// </summary>
    [BsonElement("agreement_content")]
    public string? AgreementContent { get; set; }
}
