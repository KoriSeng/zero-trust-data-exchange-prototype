namespace ZeroTrust.Backend.Models;

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

/// <summary>
/// Represents a role in the system (Requester, DataOwner, Admin, Auditor)
/// </summary>
[BsonIgnoreExtraElements]
public class Role
{
    /// <summary>
    /// Role ID (UUID)
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Role name (unique)
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Role creation timestamp
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Well-known role names
/// </summary>
public static class RoleNames
{
    public const string Requester = "Requester";
    public const string DataOwner = "DataOwner";
    public const string Admin = "Admin";
    public const string Auditor = "Auditor";
}
