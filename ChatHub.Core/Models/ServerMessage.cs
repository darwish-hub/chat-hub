using System.Text.Json.Serialization;

namespace ChatHub.Core.Models;

/// <summary>
/// Base class for all server-to-client messages
/// </summary>
public abstract record ServerMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public record MessageReceived : ServerMessage
{
    public override string Type => "message_received";

    [JsonPropertyName("envelope")]
    public MessageEnvelope Envelope { get; init; } = null!;
}

public record VoiceChunkRelay : ServerMessage
{
    public override string Type => "voice_chunk";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("sequenceNumber")]
    public int SequenceNumber { get; init; }

    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; init; }

    [JsonPropertyName("fromUserId")]
    public string FromUserId { get; init; } = string.Empty;
}

public record UserJoined : ServerMessage
{
    public override string Type => "user_joined";

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}

public record UserLeft : ServerMessage
{
    public override string Type => "user_left";

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;
}

public record TypingIndicator : ServerMessage
{
    public override string Type => "typing";

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; init; }
}

public record DeliveredReceipt : ServerMessage
{
    public override string Type => "delivered";

    [JsonPropertyName("messageId")]
    public string MessageId { get; init; } = string.Empty;
}

public record ErrorMessage : ServerMessage
{
    public override string Type => "error";

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}

public record PingMessage : ServerMessage
{
    public override string Type => "ping";
}
