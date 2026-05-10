using System.Text.Json.Serialization;

namespace ChatHub.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JoinServiceMessage), typeDiscriminator: "join_service")]
[JsonDerivedType(typeof(LeaveServiceMessage), typeDiscriminator: "leave_service")]
[JsonDerivedType(typeof(TextMessage), typeDiscriminator: "text_message")]
[JsonDerivedType(typeof(VoiceChunkMessage), typeDiscriminator: "voice_chunk")]
[JsonDerivedType(typeof(VoiceMessage), typeDiscriminator: "voice_message")]
[JsonDerivedType(typeof(FileAttachmentMessage), typeDiscriminator: "file_attachment")]
[JsonDerivedType(typeof(TypingMessage), typeDiscriminator: "typing")]
[JsonDerivedType(typeof(AckMessage), typeDiscriminator: "ack")]
[JsonDerivedType(typeof(PongMessage), typeDiscriminator: "pong")]
public abstract class ClientMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public class JoinServiceMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "join_service";

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = null!;
}

public class LeaveServiceMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "leave_service";

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = null!;
}

public class TextMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "text_message";

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = null!;

    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; set; }
}

public class VoiceChunkMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "voice_chunk";

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;

    [JsonPropertyName("sequenceNumber")]
    public int SequenceNumber { get; set; }

    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; set; }
}

public class VoiceMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "voice_message";

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;

    [JsonPropertyName("blobId")]
    public string BlobId { get; set; } = null!;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = null!;

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; set; }
}

public class FileAttachmentMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "file_attachment";

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;

    [JsonPropertyName("blobId")]
    public string BlobId { get; set; } = null!;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = null!;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = null!;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("durationMs")]
    public int? DurationMs { get; set; }

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; set; }
}

public class TypingMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "typing";

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; set; }
}

public class AckMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "ack";

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = null!;
}

public class PongMessage : ClientMessage
{
    [JsonPropertyName("type")]
    public override string Type => "pong";
}
