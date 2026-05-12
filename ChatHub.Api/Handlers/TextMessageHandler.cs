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
    private readonly INatsBackplane _natsBackplane;
    private readonly ILogger<TextMessageHandler> _logger;

    public TextMessageHandler(
        IConnectionRegistry connectionRegistry,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IRateLimiter rateLimiter,
        MongoWriterService mongoWriter,
        IWebSocketSender webSocketSender,
        INatsBackplane natsBackplane,
        ILogger<TextMessageHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
        _mongoWriter = mongoWriter;
        _webSocketSender = webSocketSender;
        _natsBackplane = natsBackplane;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, TextMessage message, CancellationToken ct)
    {
        _logger.LogDebug("TextMessageHandler: Starting to handle message {MessageId} from connection {ConnectionId}", message.Id, connectionId);

        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("TextMessageHandler: Connection {ConnectionId} not found for text message {MessageId}", connectionId, message.Id);
            await SendErrorAsync(connectionId, "not_participant", "Connection not found", message.Id);
            return;
        }
        _logger.LogDebug("TextMessageHandler: Connection {ConnectionId} found for user {UserId}", connectionId, connection.UserId);

        if (!await _rateLimiter.CanSendTextAsync(connectionId, ct))
        {
            _logger.LogWarning("TextMessageHandler: Rate limit exceeded for connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "rate_limit_exceeded", "Too many messages sent", message.Id);
            return;
        }
        _logger.LogDebug("TextMessageHandler: Rate limit check passed for connection {ConnectionId}", connectionId);

        if (!await _conversationRepository.IsParticipantAsync(message.ConversationId, connection.UserId, ct))
        {
            _logger.LogWarning("TextMessageHandler: User {UserId} is not a participant in conversation {ConversationId}",
                connection.UserId, message.ConversationId);
            await SendErrorAsync(connectionId, "not_participant", "You are not a participant in this conversation", message.Id);
            return;
        }
        _logger.LogDebug("TextMessageHandler: User {UserId} is participant in conversation {ConversationId}", connection.UserId, message.ConversationId);

        if (string.IsNullOrWhiteSpace(message.Text) || message.Text.Length > 10000)
        {
            _logger.LogWarning("TextMessageHandler: Invalid message text from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "invalid_message", "Message text is empty or exceeds maximum length", message.Id);
            return;
        }
        _logger.LogDebug("TextMessageHandler: Message text validation passed for connection {ConnectionId}", connectionId);

        if (!string.IsNullOrEmpty(message.ReplyToId))
        {
            var originalMessage = await _messageRepository.GetByIdAsync(message.ReplyToId, ct);
            if (originalMessage == null)
            {
                _logger.LogWarning("TextMessageHandler: ReplyToId {ReplyToId} not found for message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "The message you are replying to does not exist", message.Id);
                return;
            }

            if (originalMessage.ConversationId != message.ConversationId)
            {
                _logger.LogWarning("TextMessageHandler: ReplyToId {ReplyToId} is in a different conversation for message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "Cannot reply to a message from a different conversation", message.Id);
                return;
            }
            _logger.LogDebug("TextMessageHandler: ReplyToId {ReplyToId} validation passed", message.ReplyToId);
        }

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

        _logger.LogDebug("TextMessageHandler: Created message document {MessageId} in conversation {ConversationId}", messageDocument.Id, messageDocument.ConversationId);

        await _rateLimiter.RecordTextAsync(connectionId, ct);
        _logger.LogDebug("TextMessageHandler: Rate limit recorded for connection {ConnectionId}", connectionId);

        await _mongoWriter.Writer.WriteAsync(messageDocument, ct);
        _logger.LogInformation("TextMessageHandler: Message {MessageId} from user {UserId} queued for persistence",
            messageDocument.Id, connection.UserId);

        var deliveredReceipt = new DeliveredReceipt
        {
            MessageId = message.Id
        };

        var receiptJson = JsonSerializer.Serialize(deliveredReceipt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        _logger.LogDebug("TextMessageHandler: Sending delivered receipt to connection {ConnectionId}", connectionId);
        await _webSocketSender.SendTextAsync(connectionId, System.Text.Encoding.UTF8.GetBytes(receiptJson), ct);
        _logger.LogDebug("TextMessageHandler: Delivered receipt sent to connection {ConnectionId}", connectionId);

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
        _logger.LogDebug("TextMessageHandler: Serialized message envelope {MessageId}, {ByteCount} bytes", messageDocument.Id, bytes.Length);

        var conversation = await _conversationRepository.GetByIdAsync(message.ConversationId, ct);
        if (conversation != null)
        {
            _logger.LogDebug("TextMessageHandler: Found conversation {ConversationId} with {ParticipantCount} participants",
                conversation.Id, conversation.ParticipantIds.Count);
            foreach (var participantId in conversation.ParticipantIds)
            {
                if (participantId == connection.UserId)
                {
                    _logger.LogDebug("TextMessageHandler: Skipping sender {SenderId} for local broadcast", connection.UserId);
                    continue;
                }

                _logger.LogDebug("TextMessageHandler: Looking up connections for participant {ParticipantId}", participantId);
                var participantConnections = _connectionRegistry.GetByUser(participantId);
                var connList = participantConnections.ToList();
                _logger.LogDebug("TextMessageHandler: Found {ConnectionCount} connections for participant {ParticipantId}",
                    connList.Count, participantId);

                foreach (var conn in connList)
                {
                    _logger.LogDebug("TextMessageHandler: Sending message {MessageId} to connection {TargetConnectionId} for participant {ParticipantId}",
                        messageDocument.Id, conn.ConnectionId, participantId);
                    await _webSocketSender.SendTextAsync(conn.ConnectionId, bytes, ct);
                    _logger.LogDebug("TextMessageHandler: Message {MessageId} sent to connection {TargetConnectionId}",
                        messageDocument.Id, conn.ConnectionId);
                }
            }
        }
        else
        {
            _logger.LogWarning("TextMessageHandler: Conversation {ConversationId} not found for message {MessageId}",
                message.ConversationId, messageDocument.Id);
        }

        var natsSubject = $"chathub.{message.ServiceId}.messages";
        _logger.LogDebug("TextMessageHandler: Publishing to NATS subject {NatsSubject}", natsSubject);
        await _natsBackplane.PublishAsync(natsSubject, bytes, ct);
        _logger.LogDebug("TextMessageHandler: Published message {MessageId} to NATS subject {NatsSubject}",
            messageDocument.Id, natsSubject);

        await _conversationRepository.UpdateLastMessageAtAsync(message.ConversationId, DateTime.UtcNow, ct);
        _logger.LogDebug("TextMessageHandler: Updated last message at for conversation {ConversationId}", message.ConversationId);
        _logger.LogInformation("TextMessageHandler: Message {MessageId} fully processed", messageDocument.Id);
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
