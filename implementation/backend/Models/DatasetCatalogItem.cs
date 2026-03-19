using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZeroTrust.Backend.Models;

/// <summary>
/// Catalog metadata used by the dataset explorer UI.
/// </summary>
[BsonIgnoreExtraElements]
public class DatasetCatalogItem
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = null!;

    [BsonElement("dataset_id")]
    public string DatasetId { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("summary")]
    public string Summary { get; set; } = null!;

    [BsonElement("data_owner_org")]
    public string DataOwnerOrg { get; set; } = null!;

    [BsonElement("data_owner_org_short_name")]
    public string DataOwnerOrgShortName { get; set; } = null!;

    [BsonElement("data_owner_org_name")]
    public string DataOwnerOrgName { get; set; } = null!;

    [BsonElement("object_keys")]
    public List<string> ObjectKeys { get; set; } = new();

    [BsonElement("thumbnail_object_key")]
    public string? ThumbnailObjectKey { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("record_count")]
    public int RecordCount { get; set; }

    [BsonElement("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("is_published")]
    public bool IsPublished { get; set; } = true;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
