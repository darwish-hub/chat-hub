using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

/// <summary>
/// MongoDB document for connection audit logging
/// </summary>
public class ConnectionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("connectionId")]
    public string ConnectionId { get; set; } = string.Empty;

    [BsonElement("serviceId")]
    public string? ServiceId { get; set; }

    [BsonElement("podId")]
    public string PodId { get; set; } = string.Empty;

    [BsonElement("connectedAt")]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("disconnectedAt")]
    public DateTime? DisconnectedAt { get; set; }
}
