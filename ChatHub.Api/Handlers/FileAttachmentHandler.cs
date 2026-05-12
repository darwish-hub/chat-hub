using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles file attachment messages - persists metadata and broadcasts to participants.
/// Supports voice, video, images, and generic files. The message type is inferred from MIME type.
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
        _logger.LogDebug("FileAttachmentHandler: Starting to handle message {MessageId} from connection {ConnectionId}", message.Id, connectionId);

        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("FileAttachmentHandler: Connection {ConnectionId} not found for file attachment", connectionId);
            await SendErrorAsync(connectionId, "not_participant", "Connection not found", message.Id);
            return;
        }
        _logger.LogDebug("FileAttachmentHandler: Connection {ConnectionId} found for user {UserId}", connectionId, connection.UserId);

        // Rate limiting check
        if (!await _rateLimiter.CanSendTextAsync(connectionId, ct))
        {
            _logger.LogWarning("FileAttachmentHandler: Rate limit exceeded for attachments on connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "rate_limit_exceeded", "Too many attachments sent", message.Id);
            return;
        }
        _logger.LogDebug("FileAttachmentHandler: Rate limit check passed for connection {ConnectionId}", connectionId);

        // Validate conversation membership
        if (!await _conversationRepository.IsParticipantAsync(message.ConversationId, connection.UserId, ct))
        {
            _logger.LogWarning("FileAttachmentHandler: User {UserId} is not a participant in conversation {ConversationId}",
                connection.UserId, message.ConversationId);
            await SendErrorAsync(connectionId, "not_participant", "You are not a participant in this conversation", message.Id);
            return;
        }
        _logger.LogDebug("FileAttachmentHandler: User {UserId} is participant in conversation {ConversationId}", connection.UserId, message.ConversationId);

        // Validate attachment metadata
        if (string.IsNullOrWhiteSpace(message.BlobId))
        {
            _logger.LogWarning("FileAttachmentHandler: Attachment missing blobId from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_attachment", "Attachment blobId is required", message.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.FileName))
        {
            _logger.LogWarning("FileAttachmentHandler: Attachment missing filename from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_attachment", "Attachment file name is required", message.Id);
            return;
        }

        if (message.SizeBytes <= 0 || message.SizeBytes > 104_857_600) // 100 MB
        {
            _logger.LogWarning("FileAttachmentHandler: Attachment has invalid size {SizeBytes} from connection {ConnectionId}",
                message.SizeBytes, connectionId);
            await SendErrorAsync(connectionId, "invalid_attachment", "Attachment size is invalid", message.Id);
            return;
        }
        _logger.LogDebug("FileAttachmentHandler: Attachment metadata validated - fileName={FileName}, blobId={BlobId}, size={SizeBytes}",
            message.FileName, message.BlobId, message.SizeBytes);

        // Validate replyToId if provided
        if (!string.IsNullOrEmpty(message.ReplyToId))
        {
            var originalMessage = await _messageRepository.GetByIdAsync(message.ReplyToId, ct);
            if (originalMessage == null)
            {
                _logger.LogWarning("FileAttachmentHandler: ReplyToId {ReplyToId} not found for attachment from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "The message you are replying to does not exist", message.Id);
                return;
            }

            if (originalMessage.ConversationId != message.ConversationId)
            {
                _logger.LogWarning("FileAttachmentHandler: ReplyToId {ReplyToId} is in a different conversation for attachment from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "Cannot reply to a message from a different conversation", message.Id);
                return;
            }
            _logger.LogDebug("FileAttachmentHandler: ReplyToId {ReplyToId} validation passed", message.ReplyToId);
        }

        // Infer message type from MIME type
        var messageType = InferMessageType(message.MimeType);
        _logger.LogDebug("FileAttachmentHandler: Inferred message type {MessageType} from MIME {MimeType}", messageType, message.MimeType);

        // Record rate limit usage
        await _rateLimiter.RecordTextAsync(connectionId, ct);

        // Create message document with attachment metadata
        var messageDocument = new MessageDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ConversationId = message.ConversationId,
            ServiceId = message.ConversationId, // Use conversation ID as service for now
            SenderId = connection.UserId,
            Type = messageType,
            Attachment = new AttachmentMetadata
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes,
                DurationMs = message.DurationMs
            },
            ReplyToId = message.ReplyToId,
            CreatedAt = DateTime.UtcNow
        };
        _logger.LogDebug("FileAttachmentHandler: Created message document {MessageId}", messageDocument.Id);

        // Write to MongoDB via channel (will trigger NATS publish)
        await _mongoWriter.Writer.WriteAsync(messageDocument, ct);

        _logger.LogInformation("FileAttachmentHandler: Attachment {FileName} ({BlobId}) type={MessageType} from user {UserId} queued for persistence",
            message.FileName, message.BlobId, messageType, connection.UserId);

        // Send delivery confirmation to sender
        var deliveredReceipt = new DeliveredReceipt
        {
            MessageId = message.Id
        };

        var receiptJson = JsonSerializer.Serialize(deliveredReceipt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        _logger.LogDebug("FileAttachmentHandler: Sending delivered receipt to connection {ConnectionId}", connectionId);
        await _webSocketSender.SendTextAsync(connectionId, System.Text.Encoding.UTF8.GetBytes(receiptJson), ct);

        // Broadcast to other connections on this pod immediately
        var envelope = new MessageEnvelope
        {
            Id = messageDocument.Id,
            ConversationId = message.ConversationId,
            ServiceId = messageDocument.ServiceId,
            SenderId = connection.UserId,
            Type = messageType,
            Attachment = new AttachmentInfo
            {
                BlobId = message.BlobId,
                FileName = message.FileName,
                MimeType = message.MimeType,
                SizeBytes = message.SizeBytes,
                DurationMs = message.DurationMs
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
            _logger.LogDebug("FileAttachmentHandler: Broadcasting to service {ServiceId}", serviceId);
            var connections = _connectionRegistry.GetByService(serviceId).ToList();
            _logger.LogDebug("FileAttachmentHandler: Found {ConnectionCount} connections in service {ServiceId}", connections.Count, serviceId);
            foreach (var conn in connections)
            {
                if (conn.ConnectionId != connectionId)
                {
                    _logger.LogDebug("FileAttachmentHandler: Sending attachment {MessageId} to connection {TargetConnectionId}",
                        messageDocument.Id, conn.ConnectionId);
                    await _webSocketSender.SendTextAsync(conn.ConnectionId, bytes, ct);
                }
            }
        }
        else
        {
            _logger.LogWarning("FileAttachmentHandler: Connection {ConnectionId} has no CurrentServiceId", connectionId);
        }

        // Update conversation last message time
        await _conversationRepository.UpdateLastMessageAtAsync(message.ConversationId, DateTime.UtcNow, ct);

        _logger.LogInformation("FileAttachmentHandler: Attachment {FileName} processed and broadcast successfully as type {MessageType}",
            message.FileName, messageType);
    }

    /// <summary>
    /// Infer the message type from the MIME type of the attachment.
    /// </summary>
    private static string InferMessageType(string mimeType)
    {
        if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return "voice";
        if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return "video";
        return "file";
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
