using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

/// <summary>
/// MongoDB document for conversations
/// </summary>
public class ConversationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [BsonElement("participantIds")]
    public List<string> ParticipantIds { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastMessageAt")]
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
}
