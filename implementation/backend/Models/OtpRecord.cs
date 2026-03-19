namespace ZeroTrust.Backend.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class OtpRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [BsonElement("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the OTP code. Plaintext OTP values are never stored.
    /// </summary>
    [BsonElement("code_hash")]
    public string CodeHash { get; set; } = string.Empty;

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("attempt_count")]
    public int AttemptCount { get; set; }

    [BsonElement("locked_at")]
    public DateTime? LockedAt { get; set; }

    [BsonElement("used")]
    public bool Used { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
