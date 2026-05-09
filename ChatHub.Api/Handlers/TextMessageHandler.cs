using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

public class TextMessageHandler : ITextMessageHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRateLimiter _rateLimiter;
    private readonly MongoWriterService _mongoWriter;
    private readonly IWebSocketSender _webSocketSender;
    private readonly ILogger<TextMessageHandler> _logger;

    public TextMessageHandler(
        IConnectionRegistry connectionRegistry,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IRateLimiter rateLimiter,
        MongoWriterService mongoWriter,
        IWebSocketSender webSocketSender,
        ILogger<TextMessageHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
        _mongoWriter = mongoWriter;
        _webSocketSender = webSocketSender;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, TextMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for text message", connectionId);
            await SendErrorAsync(connectionId, "not_participant", "Connection not found", message.Id);
            return;
        }

        // Rate limiting check
        if (!await _rateLimiter.CanSendTextAsync(connectionId, ct))
        {
            _logger.LogWarning("Rate limit exceeded for connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "rate_limit_exceeded", "Too many messages sent", message.Id);
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

        // Validate message text
        if (string.IsNullOrWhiteSpace(message.Text) || message.Text.Length > 10000)
        {
            _logger.LogWarning("Invalid message text from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_message", "Message text is empty or exceeds maximum length", message.Id);
            return;
        }

        // Validate replyToId if provided
        if (!string.IsNullOrEmpty(message.ReplyToId))
        {
            var originalMessage = await _messageRepository.GetByIdAsync(message.ReplyToId, ct);
            if (originalMessage == null)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} not found for message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "The message you are replying to does not exist", message.Id);
                return;
            }
            
            if (originalMessage.ConversationId != message.ConversationId)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} is in a different conversation for message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "Cannot reply to a message from a different conversation", message.Id);
                return;
            }
        }

        // Create message document
        var messageDocument = new MessageDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ConversationId = message.ConversationId,
            ServiceId = message.ServiceId,
            SenderId = connection.UserId,
            Type = "text",
            Text = message.Text,
            ReplyToId = message.ReplyToId,
            CreatedAt = DateTime.UtcNow
        };

        // Record rate limit usage
        await _rateLimiter.RecordTextAsync(connectionId, ct);

        // Write to MongoDB via channel (will trigger NATS publish in MongoWriterService)
        await _mongoWriter.Writer.WriteAsync(messageDocument, ct);

        _logger.LogInformation("Message {MessageId} from user {UserId} queued for persistence",
            messageDocument.Id, connection.UserId);

        // Send delivery confirmation to sender immediately
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
        // (NATS subscriber will handle cross-pod delivery)
        var envelope = new MessageEnvelope
        {
            Id = messageDocument.Id,
            ConversationId = message.ConversationId,
            ServiceId = message.ServiceId,
            SenderId = connection.UserId,
            Type = "text",
            Text = message.Text,
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
        var connections = _connectionRegistry.GetByService(message.ServiceId);
        foreach (var conn in connections)
        {
            if (conn.ConnectionId != connectionId)
            {
                await _webSocketSender.SendTextAsync(conn.ConnectionId, bytes, ct);
            }
        }

        // Update conversation last message time
        await _conversationRepository.UpdateLastMessageAtAsync(message.ConversationId, DateTime.UtcNow, ct);
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
