using MongoDB.Bson.Serialization.Attributes;

namespace ZeroTrust.Backend.Models;

/// <summary>
/// User agreement with versioning for audit trail
/// </summary>
[BsonIgnoreExtraElements]
public class UserAgreement
{
    /// <summary>
    /// Agreement ID (UUID)
    /// </summary>
    [BsonElement("agreement_id")]
    public string AgreementId { get; set; } = null!;

    /// <summary>
    /// Agreement version (1, 2, 3, etc.)
    /// Composite key with AgreementId
    /// </summary>
    [BsonElement("version")]
    public int Version { get; set; }

    /// <summary>
    /// Agreement name (e.g., "Data Access Terms")
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Full agreement text/HTML (can be large)
    /// </summary>
    [BsonElement("content")]
    public string Content { get; set; } = null!;

    /// <summary>
    /// When agreement becomes effective
    /// </summary>
    [BsonElement("effective_date")]
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// When agreement expires (null = indefinite)
    /// </summary>
    [BsonElement("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Agreement status
    /// </summary>
    [BsonElement("status")]
    public AgreementStatus Status { get; set; } = AgreementStatus.Active;

    /// <summary>
    /// Timestamp when created
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Admin who created the agreement
    /// </summary>
    [BsonElement("created_by")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Organization that created agreement (null = system-wide)
    /// </summary>
    [BsonElement("organization_id")]
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Agreement status enumeration
/// </summary>
public enum AgreementStatus
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Active,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Archived,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Deprecated
}
