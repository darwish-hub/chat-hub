using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

public class ConnectionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("userId")]
    public string UserId { get; set; } = null!;
    
    [BsonElement("connectionId")]
    public string ConnectionId { get; set; } = null!;
    
    [BsonElement("serviceId")]
    public string? ServiceId { get; set; }
    
    [BsonElement("podId")]
    public string PodId { get; set; } = null!;
    
    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }
    
    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
    
    [BsonElement("connectedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("disconnectedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DisconnectedAt { get; set; }
}
