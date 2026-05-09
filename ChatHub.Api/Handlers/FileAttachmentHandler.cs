using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles file attachment messages - persists metadata and broadcasts to participants
/// </summary>
public class FileAttachmentHandler : IFileAttachmentHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRateLimiter _rateLimiter;
    private readonly MongoWriterService _mongoWriter;
    private readonly IWebSocketSender _webSocketSender;
    private readonly ILogger<FileAttachmentHandler> _logger;

    public FileAttachmentHandler(
        IConnectionRegistry connectionRegistry,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IRateLimiter rateLimiter,
        MongoWriterService mongoWriter,
        IWebSocketSender webSocketSender,
        ILogger<FileAttachmentHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
        _mongoWriter = mongoWriter;
        _webSocketSender = webSocketSender;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, FileAttachmentMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for file attachment", connectionId);
            await SendErrorAsync(connectionId, "not_participant", "Connection not found", message.Id);
            return;
        }

        // Rate limiting check
        if (!await _rateLimiter.CanSendTextAsync(connectionId, ct))
        {
            _logger.LogWarning("Rate limit exceeded for file attachments on connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "rate_limit_exceeded", "Too many file attachments sent", message.Id);
            return;
        }

        // Validate conversation membership
        if (!await _conversationRepository.IsParticipantAsync(message.ConversationId, connection.UserId, ct))
        {
            _logger.LogWarning("User {UserId} is not a participant in conversation {ConversationId}",
                connection.UserId, message.ConversationId);
            await SendErrorAsync(connectionId, "not_participant", "You are not a participant in this conversation", message.Id);
            return;
        }

        // Validate file metadata
        if (string.IsNullOrWhiteSpace(message.BlobId))
        {
            _logger.LogWarning("File attachment missing blobId from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_file", "File blobId is required", message.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.FileName))
        {
            _logger.LogWarning("File attachment missing filename from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_file", "File name is required", message.Id);
            return;
        }

        if (message.SizeBytes <= 0 || message.SizeBytes > 104_857_600) // 100 MB
        {
            _logger.LogWarning("File attachment has invalid size {SizeBytes} from connection {ConnectionId}",
                message.SizeBytes, connectionId);
            await SendErrorAsync(connectionId, "invalid_file", "File size is invalid", message.Id);
            return;
        }

        // Validate replyToId if provided
        if (!string.IsNullOrEmpty(message.ReplyToId))
        {
            var originalMessage = await _messageRepository.GetByIdAsync(message.ReplyToId, ct);
            if (originalMessage == null)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} not found for file attachment from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "The message you are replying to does not exist", message.Id);
                return;
            }
            
            if (originalMessage.ConversationId != message.ConversationId)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} is in a different conversation for file attachment from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "Cannot reply to a message from a different conversation", message.Id);
                return;
            }
        }

        // Record rate limit usage
        await _rateLimiter.RecordTextAsync(connectionId, ct);

        // Create message document with file metadata
        var messageDocument = new MessageDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ConversationId = message.ConversationId,
            ServiceId = message.ConversationId, // Use conversation ID as service for now
            SenderId = connection.UserId,
            Type = "file",
            File = new FileMetadata
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes
            },
            ReplyToId = message.ReplyToId,
            CreatedAt = DateTime.UtcNow
        };

        // Write to MongoDB via channel (will trigger NATS publish)
        await _mongoWriter.Writer.WriteAsync(messageDocument, ct);

        _logger.LogInformation("File attachment {FileName} ({BlobId}) from user {UserId} queued for persistence",
            message.FileName, message.BlobId, connection.UserId);

        // Send delivery confirmation to sender
        var deliveredReceipt = new DeliveredReceipt
        {
            MessageId = message.Id
        };

        var receiptJson = JsonSerializer.Serialize(deliveredReceipt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await _webSocketSender.SendTextAsync(connectionId, System.Text.Encoding.UTF8.GetBytes(receiptJson), ct);

        // Broadcast to other connections on this pod immediately
        var envelope = new MessageEnvelope
        {
            Id = messageDocument.Id,
            ConversationId = message.ConversationId,
            ServiceId = messageDocument.ServiceId,
            SenderId = connection.UserId,
            Type = "file",
            File = new FileInfo
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes
            },
            ReplyToId = message.ReplyToId,
            CreatedAt = messageDocument.CreatedAt
        };

        var messageReceived = new MessageReceived
        {
            Envelope = envelope
        };

        var json = JsonSerializer.Serialize(messageReceived, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Send to all connections in the service except sender
        var serviceId = connection.CurrentServiceId;
        if (!string.IsNullOrEmpty(serviceId))
        {
            var connections = _connectionRegistry.GetByService(serviceId);
            foreach (var conn in connections)
            {
                if (conn.ConnectionId != connectionId)
                {
                    await _webSocketSender.SendTextAsync(conn.ConnectionId, bytes, ct);
                }
            }
        }

        // Update conversation last message time
        await _conversationRepository.UpdateLastMessageAtAsync(message.ConversationId, DateTime.UtcNow, ct);

        _logger.LogInformation("File attachment {FileName} processed and broadcast successfully", message.FileName);
    }

    private async Task SendErrorAsync(string connectionId, string code, string errorMessage, string correlationId)
    {
        var error = new ErrorMessage
        {
            Code = code,
            Message = errorMessage,
            CorrelationId = correlationId
        };

        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        await _webSocketSender.SendTextAsync(connectionId, bytes, CancellationToken.None);
    }
}
