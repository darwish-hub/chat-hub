using System.Text.Json.Serialization;

namespace ChatHub.Core.Models;

/// <summary>
/// Message envelope sent to clients containing full message details
/// </summary>
public record MessageEnvelope
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("conversationId")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    [JsonPropertyName("senderId")]
    public string SenderId { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("voice")]
    public VoiceInfo? Voice { get; init; }

    [JsonPropertyName("file")]
    public FileInfo? File { get; init; }

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}

public record VoiceInfo
{
    [JsonPropertyName("blobId")]
    public string BlobId { get; init; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;
}

public record FileInfo
{
    [JsonPropertyName("blobId")]
    public string BlobId { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }
}
