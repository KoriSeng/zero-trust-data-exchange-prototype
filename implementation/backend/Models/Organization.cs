using MongoDB.Bson.Serialization.Attributes;

namespace ZeroTrust.Backend.Models;

/// <summary>
/// Represents an organization with Cognito group mapping
/// </summary>
[BsonIgnoreExtraElements]
public class Organization
{
    /// <summary>
    /// Organization ID (e.g., "ORG-A", "ORG-B")
    /// </summary>
    [BsonId]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Organization display name
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Cognito IDP name (e.g., "IDP-A")
    /// </summary>
    [BsonElement("cognito_idp_name")]
    public string? CognitoIdpName { get; set; }

    /// <summary>
    /// Auto-generated Cognito group name
    /// (e.g., "ap-southeast-1_lq8PdxNkF_IDP-B")
    /// Used for JIT user provisioning
    /// </summary>
    [BsonElement("cognito_group_name")]
    public string CognitoGroupName { get; set; } = null!;

    /// <summary>
    /// OIDC issuer URL
    /// </summary>
    [BsonElement("idp_issuer")]
    public string? IdpIssuer { get; set; }

    /// <summary>
    /// Organization status
    /// </summary>
    [BsonElement("status")]
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    /// <summary>
    /// Timestamp of onboarding
    /// </summary>
    [BsonElement("onboarded_at")]
    public DateTime OnboardedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Admin who onboarded the organization
    /// </summary>
    [BsonElement("onboarded_by")]
    public string? OnboardedBy { get; set; }

    /// <summary>
    /// Whether NDA is verified
    /// </summary>
    [BsonElement("nda_verified")]
    public bool NdaVerified { get; set; }

    /// <summary>
    /// NDA expiration date
    /// </summary>
    [BsonElement("nda_expires_at")]
    public DateTime? NdaExpiresAt { get; set; }
}

/// <summary>
/// Organization status enumeration
/// </summary>
public enum OrganizationStatus
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Active,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Suspended,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Deprovisioned
}
