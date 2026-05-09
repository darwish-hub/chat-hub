using System.Text.Json.Serialization;

namespace ChatHub.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MessageReceived), typeDiscriminator: "message_received")]
[JsonDerivedType(typeof(VoiceChunkReceived), typeDiscriminator: "voice_chunk")]
[JsonDerivedType(typeof(UserJoined), typeDiscriminator: "user_joined")]
[JsonDerivedType(typeof(UserLeft), typeDiscriminator: "user_left")]
[JsonDerivedType(typeof(TypingIndicator), typeDiscriminator: "typing")]
[JsonDerivedType(typeof(DeliveredReceipt), typeDiscriminator: "delivered")]
[JsonDerivedType(typeof(ErrorMessage), typeDiscriminator: "error")]
[JsonDerivedType(typeof(PingMessage), typeDiscriminator: "ping")]
public abstract class ServerMessage
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public class MessageReceived : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "message_received";
    
    [JsonPropertyName("envelope")]
    public MessageEnvelope Envelope { get; set; } = null!;
}

// MessageEnvelope, VoiceInfo, FileInfo are defined in separate files

public class VoiceChunkReceived : ServerMessage
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
    
    [JsonPropertyName("fromUserId")]
    public string FromUserId { get; set; } = null!;
}

public class UserJoined : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "user_joined";
    
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = null!;
    
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = null!;
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = null!;
}

public class UserLeft : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "user_left";
    
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = null!;
    
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = null!;
}

public class TypingIndicator : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "typing";
    
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = null!;
    
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = null!;
    
    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; set; }
}

public class DeliveredReceipt : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "delivered";
    
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = null!;
}

public class ErrorMessage : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "error";
    
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;
    
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

public class PingMessage : ServerMessage
{
    [JsonPropertyName("type")]
    public override string Type => "ping";
}
