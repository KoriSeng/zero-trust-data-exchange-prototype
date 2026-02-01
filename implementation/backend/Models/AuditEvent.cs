using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZeroTrust.Backend.Models;

/// <summary>
/// Audit event for logging all actions in the system
/// Immutable append-only log for compliance and security investigation
/// </summary>
[BsonIgnoreExtraElements]
public class AuditEvent
{
    /// <summary>
    /// Unique identifier for this audit event
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of action (e.g., "REQUEST_SUBMITTED", "REQUEST_APPROVED", "OTP_REDEEMED", "DATA_ACCESSED")
    /// </summary>
    [BsonElement("event_type")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// User who performed the action
    /// </summary>
    [BsonElement("actor_id")]
    public string? ActorId { get; set; }

    /// <summary>
    /// Organization context for the action
    /// </summary>
    [BsonElement("organization_id")]
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Related request ID (if applicable)
    /// </summary>
    [BsonElement("request_id")]
    public string? RequestId { get; set; }

    /// <summary>
    /// Related dataset ID (if applicable)
    /// </summary>
    [BsonElement("dataset_id")]
    public string? DatasetId { get; set; }

    /// <summary>
    /// Action description for human readability
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Structured metadata about the event
    /// Can contain additional context like IP address, user agent, data accessed, etc.
    /// </summary>
    [BsonElement("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Result of the action (Success, Failure, PartialSuccess)
    /// </summary>
    [BsonElement("result")]
    [BsonRepresentation(BsonType.String)]
    public AuditEventResult Result { get; set; } = AuditEventResult.Success;

    /// <summary>
    /// Error message if action failed
    /// </summary>
    [BsonElement("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp of the event (UTC)
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit event result enumeration
/// </summary>
public enum AuditEventResult
{
    [BsonRepresentation(BsonType.String)]
    Success,
    [BsonRepresentation(BsonType.String)]
    Failure,
    [BsonRepresentation(BsonType.String)]
    PartialSuccess
}
