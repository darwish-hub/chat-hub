using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatHub.Core.Documents;

public class MessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = null!;

    [BsonElement("serviceId")]
    public string ServiceId { get; set; } = null!;

    [BsonElement("senderId")]
    public string SenderId { get; set; } = null!;

    [BsonElement("type")]
    public string Type { get; set; } = null!; // "text", "voice", "video", "file"

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("attachment")]
    public AttachmentMetadata? Attachment { get; set; }

    [BsonElement("replyToId")]
    public string? ReplyToId { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("deliveredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DeliveredAt { get; set; }
}

public class AttachmentMetadata
{
    [BsonElement("blobId")]
    public string BlobId { get; set; } = null!;

    [BsonElement("fileName")]
    public string FileName { get; set; } = null!;

    [BsonElement("mimeType")]
    public string MimeType { get; set; } = null!;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("durationMs")]
    public int? DurationMs { get; set; }
}
