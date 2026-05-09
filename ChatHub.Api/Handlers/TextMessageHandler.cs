using System.Text.Json;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Options;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles text_message messages
/// </summary>
public class TextMessageHandler : IMessageHandler<TextMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly IRateLimiter _rateLimiter;
    private readonly MongoWriterService _writerService;
    private readonly ILogger<TextMessageHandler> _logger;
    private readonly ChatHubSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public TextMessageHandler(
        IConnectionRegistry registry,
        IRateLimiter rateLimiter,
        MongoWriterService writerService,
        IOptions<ChatHubSettings> settings,
        ILogger<TextMessageHandler> logger)
    {
        _registry = registry;
        _rateLimiter = rateLimiter;
        _writerService = writerService;
        _logger = logger;
        _settings = settings.Value;
    }
    
    public async Task HandleAsync(string connectionId, TextMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        // Check rate limit
        var rateLimitKey = $"text:{connectionId}";
        if (!await _rateLimiter.IsAllowedAsync(rateLimitKey, _settings.RateLimitTextPerMinute, TimeSpan.FromMinutes(1), ct))
        {
            _logger.LogWarning("Rate limit exceeded for connection {ConnectionId}", connectionId);
            // Could send error message back to client
            return;
        }
        
        // Create message document
        var document = new MessageDocument
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            ServiceId = message.ServiceId,
            SenderId = connection.UserId,
            Type = "text",
            Text = message.Text,
            ReplyToId = message.ReplyToId,
            CreatedAt = DateTime.UtcNow
        };
        
        // Create envelope for broadcasting
        var envelope = new MessageEnvelope
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            ServiceId = message.ServiceId,
            SenderId = connection.UserId,
            Type = "text",
            Text = message.Text,
            ReplyToId = message.ReplyToId,
            CreatedAt = document.CreatedAt
        };
        
        // Queue for background write + NATS publish
        _writerService.QueueWrite(document, envelope);
        
        _logger.LogDebug(
            "Text message {MessageId} queued for processing from user {UserId}",
            message.Id, connection.UserId);
    }
}
