namespace ZeroTrust.Backend.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Represents a user authenticated via Cognito with federated identity
/// </summary>
[BsonIgnoreExtraElements]
public class User
{
    /// <summary>
    /// Internal user ID (UUID)
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Cognito subject (unique across pool, stable identifier)
    /// </summary>
    [BsonElement("sub")]
    public string Sub { get; set; } = null!;

    /// <summary>
    /// Cognito-prefixed username (e.g., "IDP-A_a.alex")
    /// </summary>
    [BsonElement("cognito_username")]
    public string? CognitoUsername { get; set; }

    /// <summary>
    /// Original IdP subject (e.g., "A-001")
    /// </summary>
    [BsonElement("federated_sub")]
    public string? FederatedSub { get; set; }

    /// <summary>
    /// Identity provider name (e.g., "IDP-A", "IDP-B")
    /// </summary>
    [BsonElement("identity_provider")]
    public string? IdentityProvider { get; set; }

    /// <summary>
    /// OIDC issuer URL
    /// </summary>
    [BsonElement("issuer")]
    public string? Issuer { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    [BsonElement("email")]
    public string? Email { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    [BsonElement("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Organization identifier (e.g., "ORG-A", "ORG-B")
    /// </summary>
    [BsonElement("organization_id")]
    public string? OrganizationId { get; set; }

    /// <summary>
    /// User status (ACTIVE, SUSPENDED, INACTIVE)
    /// </summary>
    [BsonElement("status")]
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// Timestamp of first authentication (JIT provisioning)
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of last authentication
    /// </summary>
    [BsonElement("last_access_at")]
    public DateTime LastAccessAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User status enumeration
/// </summary>
public enum UserStatus
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Active,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Suspended,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Inactive
}
