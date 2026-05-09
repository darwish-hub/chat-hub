using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

/// <summary>
/// MongoDB document for chat messages
/// </summary>
public class MessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [BsonElement("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // text, voice, file

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("voice")]
    public VoiceData? Voice { get; set; }

    [BsonElement("file")]
    public FileData? File { get; set; }

    [BsonElement("replyToId")]
    public string? ReplyToId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }
}

public class VoiceData
{
    [BsonElement("blobId")]
    public string BlobId { get; set; } = string.Empty;

    [BsonElement("durationMs")]
    public int DurationMs { get; set; }

    [BsonElement("mimeType")]
    public string MimeType { get; set; } = string.Empty;
}

public class FileData
{
    [BsonElement("blobId")]
    public string BlobId { get; set; } = string.Empty;

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }
}
