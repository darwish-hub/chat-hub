using System.Text.Json;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Options;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles file attachment messages
/// </summary>
public class FileAttachmentHandler : IMessageHandler<FileAttachmentMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly IRateLimiter _rateLimiter;
    private readonly MongoWriterService _writerService;
    private readonly ILogger<FileAttachmentHandler> _logger;
    private readonly ChatHubSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public FileAttachmentHandler(
        IConnectionRegistry registry,
        IRateLimiter rateLimiter,
        MongoWriterService writerService,
        IOptions<ChatHubSettings> settings,
        ILogger<FileAttachmentHandler> logger)
    {
        _registry = registry;
        _rateLimiter = rateLimiter;
        _writerService = writerService;
        _logger = logger;
        _settings = settings.Value;
    }
    
    public async Task HandleAsync(string connectionId, FileAttachmentMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        // Check rate limit (same as text messages)
        var rateLimitKey = $"file:{connectionId}";
        if (!await _rateLimiter.IsAllowedAsync(rateLimitKey, _settings.RateLimitTextPerMinute, TimeSpan.FromMinutes(1), ct))
        {
            _logger.LogWarning("File rate limit exceeded for connection {ConnectionId}", connectionId);
            return;
        }
        
        // Create message document
        var document = new MessageDocument
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            ServiceId = "",
            SenderId = connection.UserId,
            Type = "file",
            File = new FileData
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes
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
            Type = "file",
            File = new Core.Models.FileInfo
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes
            },
            CreatedAt = document.CreatedAt
        };
        
        _writerService.QueueWrite(document, envelope);
        
        _logger.LogInformation(
            "File attachment {MessageId} ({FileName}) queued from user {UserId}",
            message.Id, message.FileName, connection.UserId);
    }
}
