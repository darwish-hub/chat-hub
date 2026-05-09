using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

public class ConversationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("serviceId")]
    public string ServiceId { get; set; } = null!;
    
    [BsonElement("participantIds")]
    public List<string> ParticipantIds { get; set; } = new();
    
    [BsonElement("title")]
    public string? Title { get; set; }
    
    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = null!;
    
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("lastMessageAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastMessageAt { get; set; }
}
