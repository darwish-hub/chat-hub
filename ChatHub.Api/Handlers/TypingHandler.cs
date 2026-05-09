using System.Text.Json;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles typing indicator messages
/// </summary>
public class TypingHandler : IMessageHandler<TypingMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly INatsBackplane _nats;
    private readonly ILogger<TypingHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public TypingHandler(
        IConnectionRegistry registry,
        INatsBackplane nats,
        ILogger<TypingHandler> logger)
    {
        _registry = registry;
        _nats = nats;
        _logger = logger;
    }
    
    public async Task HandleAsync(string connectionId, TypingMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        // Publish typing event to NATS for cross-pod fan-out
        var typingEvent = new TypingIndicator
        {
            UserId = connection.UserId,
            ConversationId = message.ConversationId,
            IsTyping = message.IsTyping
        };
        
        var payload = JsonSerializer.SerializeToUtf8Bytes(typingEvent, JsonOptions);
        
        // Find which service this conversation belongs to and broadcast
        foreach (var serviceId in connection.JoinedServices)
        {
            await _nats.PublishAsync($"chathub.{serviceId}.presence", payload, ct);
        }
        
        _logger.LogDebug(
            "Typing indicator from user {UserId} in conversation {ConversationId}: {IsTyping}",
            connection.UserId, message.ConversationId, message.IsTyping);
    }
}
