using System.Text.Json.Serialization;

namespace ChatHub.Core.Models;

/// <summary>
/// Base class for all client-to-server messages
/// </summary>
public abstract record ClientMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public record JoinServiceMessage : ClientMessage
{
    public override string Type => "join_service";

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;
}

public record LeaveServiceMessage : ClientMessage
{
    public override string Type => "leave_service";

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;
}

public record TextMessage : ClientMessage
{
    public override string Type => "text_message";

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; init; }
}

public record VoiceChunkMessage : ClientMessage
{
    public override string Type => "voice_chunk";

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("sequenceNumber")]
    public int SequenceNumber { get; init; }

    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; init; }
}

public record VoiceMessage : ClientMessage
{
    public override string Type => "voice_message";

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("blobId")]
    public string BlobId { get; init; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = "audio/opus";
}

public record FileAttachmentMessage : ClientMessage
{
    public override string Type => "file_attachment";

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("blobId")]
    public string BlobId { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }
}

public record TypingMessage : ClientMessage
{
    public override string Type => "typing";

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; init; }
}

public record AckMessage : ClientMessage
{
    public override string Type => "ack";

    [JsonPropertyName("messageId")]
    public string MessageId { get; init; } = string.Empty;
}

public record PongMessage : ClientMessage
{
    public override string Type => "pong";
}
