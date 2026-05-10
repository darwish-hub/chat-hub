using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

public class RateLimitDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("connectionId")]
    public string ConnectionId { get; set; } = null!;

    [BsonElement("type")]
    public string Type { get; set; } = null!; // "text" or "voice"

    [BsonElement("windowStart")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime WindowStart { get; set; }

    [BsonElement("count")]
    public int Count { get; set; }

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}
