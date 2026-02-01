namespace ZeroTrust.Backend.Models;

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

/// <summary>
/// Maps users to roles (many-to-many junction)
/// </summary>
[BsonIgnoreExtraElements]
public class UserRole
{
    /// <summary>
    /// Composite ID: userId#roleId
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// User ID reference
    /// </summary>
    [BsonElement("user_id")]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Role ID reference
    /// </summary>
    [BsonElement("role_id")]
    public string RoleId { get; set; } = null!;

    /// <summary>
    /// Timestamp when role was assigned
    /// </summary>
    [BsonElement("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Admin who assigned the role
    /// </summary>
    [BsonElement("assigned_by")]
    public string? AssignedBy { get; set; }
}
