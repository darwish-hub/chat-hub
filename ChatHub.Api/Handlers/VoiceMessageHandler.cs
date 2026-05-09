using System.Text.Json;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Options;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles pre-recorded voice messages
/// </summary>
public class VoiceMessageHandler : IMessageHandler<VoiceMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly IRateLimiter _rateLimiter;
    private readonly MongoWriterService _writerService;
    private readonly ILogger<VoiceMessageHandler> _logger;
    private readonly ChatHubSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public VoiceMessageHandler(
        IConnectionRegistry registry,
        IRateLimiter rateLimiter,
        MongoWriterService writerService,
        IOptions<ChatHubSettings> settings,
        ILogger<VoiceMessageHandler> logger)
    {
        _registry = registry;
        _rateLimiter = rateLimiter;
        _writerService = writerService;
        _logger = logger;
        _settings = settings.Value;
    }
    
    public async Task HandleAsync(string connectionId, VoiceMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        // Check rate limit
        var rateLimitKey = $"voice:{connectionId}";
        if (!await _rateLimiter.IsAllowedAsync(rateLimitKey, _settings.RateLimitVoicePerMinute, TimeSpan.FromMinutes(1), ct))
        {
            _logger.LogWarning("Voice rate limit exceeded for connection {ConnectionId}", connectionId);
            return;
        }
        
        // Create message document
        var document = new MessageDocument
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            ServiceId = "", // Would be determined from conversation
            SenderId = connection.UserId,
            Type = "voice",
            Voice = new VoiceData
            {
                BlobId = message.BlobId,
                DurationMs = message.DurationMs,
                MimeType = message.MimeType
            },
            CreatedAt = DateTime.UtcNow
        };
        
        // Create envelope
        var envelope = new MessageEnvelope
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            ServiceId = document.ServiceId,
            SenderId = connection.UserId,
            Type = "voice",
            Voice = new Core.Models.VoiceInfo
            {
                BlobId = message.BlobId,
                DurationMs = message.DurationMs,
                MimeType = message.MimeType
            },
            CreatedAt = document.CreatedAt
        };
        
        _writerService.QueueWrite(document, envelope);
        
        _logger.LogInformation(
            "Voice message {MessageId} queued from user {UserId}",
            message.Id, connection.UserId);
    }
}
