namespace ZeroTrust.Backend.Models;

using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Request status enumeration
/// </summary>
public enum RequestStatus
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Draft,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Submitted,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    PendingAdminReview,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    PendingOwnerApproval,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Approved,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    OtpSent,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Redeemed,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Completed,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Denied,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Expired,
    
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    Revoked
}
