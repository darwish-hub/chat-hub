using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Cache;
using ChatHub.Infrastructure.Writers;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles completed voice messages - assembles chunks, uploads to S3, and persists metadata
/// </summary>
public class VoiceMessageHandler : IVoiceMessageHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRateLimiter _rateLimiter;
    private readonly IBlobStorageClient _blobStorage;
    private readonly VoiceSessionBuffer _voiceBuffer;
    private readonly MongoWriterService _mongoWriter;
    private readonly IWebSocketSender _webSocketSender;
    private readonly ILogger<VoiceMessageHandler> _logger;

    public VoiceMessageHandler(
        IConnectionRegistry connectionRegistry,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IRateLimiter rateLimiter,
        IBlobStorageClient blobStorage,
        VoiceSessionBuffer voiceBuffer,
        MongoWriterService mongoWriter,
        IWebSocketSender webSocketSender,
        ILogger<VoiceMessageHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
        _blobStorage = blobStorage;
        _voiceBuffer = voiceBuffer;
        _mongoWriter = mongoWriter;
        _webSocketSender = webSocketSender;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, VoiceMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for voice message", connectionId);
            await SendErrorAsync(connectionId, "not_participant", "Connection not found", message.Id);
            return;
        }

        // Rate limiting check
        if (!await _rateLimiter.CanSendVoiceAsync(connectionId, ct))
        {
            _logger.LogWarning("Rate limit exceeded for voice messages on connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "rate_limit_exceeded", "Too many voice messages sent", message.Id);
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

        // Validate replyToId if provided
        if (!string.IsNullOrEmpty(message.ReplyToId))
        {
            var originalMessage = await _messageRepository.GetByIdAsync(message.ReplyToId, ct);
            if (originalMessage == null)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} not found for voice message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "The message you are replying to does not exist", message.Id);
                return;
            }
            
            if (originalMessage.ConversationId != message.ConversationId)
            {
                _logger.LogWarning("ReplyToId {ReplyToId} is in a different conversation for voice message from connection {ConnectionId}",
                    message.ReplyToId, connectionId);
                await SendErrorAsync(connectionId, "invalid_reply", "Cannot reply to a message from a different conversation", message.Id);
                return;
            }
        }

        try
        {
            // Assemble all voice chunks from Redis
            var audioData = await _voiceBuffer.AssembleAudioAsync(message.Id, ct);
            
            if (audioData.Length == 0)
            {
                _logger.LogWarning("No voice chunks found for message {MessageId}", message.Id);
                await SendErrorAsync(connectionId, "voice_assembly_error", "No voice data found", message.Id);
                return;
            }

            // Upload assembled audio to S3
            using var audioStream = new MemoryStream(audioData);
            var blobId = message.BlobId; // Client provides blobId, or we could generate one
            
            await _blobStorage.UploadAsync(blobId, audioStream, message.MimeType, ct);
            _logger.LogInformation("Voice message {MessageId} uploaded to S3 as {BlobId}, size: {Size} bytes",
                message.Id, blobId, audioData.Length);

            // Record rate limit usage
            await _rateLimiter.RecordVoiceAsync(connectionId, ct);

            // Create message document
            var messageDocument = new MessageDocument
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ConversationId = message.ConversationId,
                ServiceId = message.ConversationId, // Use conversation ID as service for now
                SenderId = connection.UserId,
                Type = "voice",
                Voice = new VoiceMetadata
                {
                    BlobId = blobId,
                    DurationMs = message.DurationMs,
                    MimeType = message.MimeType
                },
                ReplyToId = message.ReplyToId,
                CreatedAt = DateTime.UtcNow
            };

            // Write to MongoDB via channel (will trigger NATS publish)
            await _mongoWriter.Writer.WriteAsync(messageDocument, ct);

            // Clean up Redis chunks
            await _voiceBuffer.DeleteSessionAsync(message.Id, ct);

            // Send delivery confirmation
            var deliveredReceipt = new DeliveredReceipt
            {
                MessageId = message.Id
            };

            var receiptJson = JsonSerializer.Serialize(deliveredReceipt, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await _webSocketSender.SendTextAsync(connectionId, System.Text.Encoding.UTF8.GetBytes(receiptJson), ct);

            // Broadcast to other connections on this pod
            var envelope = new MessageEnvelope
            {
                Id = messageDocument.Id,
                ConversationId = message.ConversationId,
                ServiceId = messageDocument.ServiceId,
                SenderId = connection.UserId,
                Type = "voice",
                Voice = new ChatHub.Core.Models.VoiceInfo
                {
                    BlobId = blobId,
                    DurationMs = message.DurationMs,
                    MimeType = message.MimeType
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

            _logger.LogInformation("Voice message {MessageId} processed and broadcast successfully", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice message {MessageId}", message.Id);
            await SendErrorAsync(connectionId, "voice_processing_error", "Failed to process voice message", message.Id);
        }
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
