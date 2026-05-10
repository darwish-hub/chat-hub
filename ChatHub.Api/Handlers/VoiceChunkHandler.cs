using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles live voice chunk streaming - receives chunks and forwards them to other participants
/// </summary>
public class VoiceChunkHandler : IVoiceChunkHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IWebSocketSender _webSocketSender;
    private readonly IConversationRepository _conversationRepository;
    private readonly IRateLimiter _rateLimiter;
    private readonly VoiceSessionBuffer _voiceBuffer;
    private readonly ILogger<VoiceChunkHandler> _logger;

    // Track pending binary data per connection
    private static readonly Dictionary<string, PendingVoiceChunk> _pendingChunks = new();

    public VoiceChunkHandler(
        IConnectionRegistry connectionRegistry,
        IWebSocketSender webSocketSender,
        IConversationRepository conversationRepository,
        IRateLimiter rateLimiter,
        VoiceSessionBuffer voiceBuffer,
        ILogger<VoiceChunkHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _webSocketSender = webSocketSender;
        _conversationRepository = conversationRepository;
        _rateLimiter = rateLimiter;
        _voiceBuffer = voiceBuffer;
        _logger = logger;
    }

    /// <summary>
    /// Handle voice chunk metadata (JSON message)
    /// </summary>
    public async Task HandleAsync(string connectionId, VoiceChunkMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for voice chunk", connectionId);
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

        // Store metadata for binary frame handling
        _pendingChunks[connectionId] = new PendingVoiceChunk
        {
            MessageId = message.Id,
            ConversationId = message.ConversationId,
            SequenceNumber = message.SequenceNumber,
            IsFinal = message.IsFinal,
            SenderId = connection.UserId,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug("Voice chunk metadata received for message {MessageId}, sequence {SequenceNumber}",
            message.Id, message.SequenceNumber);
    }

    /// <summary>
    /// Handle binary voice data received after the JSON metadata
    /// </summary>
    public async Task HandleBinaryDataAsync(string connectionId, byte[] audioData, CancellationToken ct)
    {
        if (!_pendingChunks.TryGetValue(connectionId, out var pending))
        {
            _logger.LogWarning("Received binary voice data for connection {ConnectionId} without pending metadata", connectionId);
            return;
        }

        // Remove from pending
        _pendingChunks.Remove(connectionId);

        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found when processing binary voice data", connectionId);
            return;
        }

        try
        {
            // Store chunk in memory for assembly later
            await _voiceBuffer.StoreChunkAsync(
                pending.MessageId,
                pending.SequenceNumber,
                audioData,
                pending.IsFinal,
                ct);

            // Forward chunk to other participants immediately
            await ForwardChunkAsync(pending, audioData, connectionId, ct);

            _logger.LogDebug("Voice chunk {SequenceNumber} for message {MessageId} processed and forwarded",
                pending.SequenceNumber, pending.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice chunk for message {MessageId}", pending.MessageId);
            await SendErrorAsync(connectionId, "voice_processing_error", "Failed to process voice chunk", pending.MessageId);
        }
    }

    /// <summary>
    /// Forward voice chunk to other participants in the conversation
    /// </summary>
    private async Task ForwardChunkAsync(PendingVoiceChunk chunk, byte[] audioData, string senderConnectionId, CancellationToken ct)
    {
        // Get all connections for the sender's service
        var serviceId = _connectionRegistry.Get(senderConnectionId)?.CurrentServiceId;
        if (string.IsNullOrEmpty(serviceId))
        {
            _logger.LogWarning("Cannot forward voice chunk - sender {SenderConnectionId} is not in a service", senderConnectionId);
            return;
        }

        // Prepare header message
        var headerMessage = new VoiceChunkReceived
        {
            Id = chunk.MessageId,
            ConversationId = chunk.ConversationId,
            SequenceNumber = chunk.SequenceNumber,
            IsFinal = chunk.IsFinal,
            FromUserId = chunk.SenderId
        };

        var headerJson = JsonSerializer.Serialize(headerMessage, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);

        // Send to all connections in the service except sender
        var connections = _connectionRegistry.GetByService(serviceId);
        foreach (var conn in connections)
        {
            if (conn.ConnectionId != senderConnectionId)
            {
                try
                {
                    // Send header first
                    await _webSocketSender.SendTextAsync(conn.ConnectionId, headerBytes, ct);
                    // Then send binary audio data
                    await _webSocketSender.SendBinaryAsync(conn.ConnectionId, audioData, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to forward voice chunk to connection {ConnectionId}", conn.ConnectionId);
                }
            }
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

    private class PendingVoiceChunk
    {
        public string MessageId { get; set; } = null!;
        public string ConversationId { get; set; } = null!;
        public int SequenceNumber { get; set; }
        public bool IsFinal { get; set; }
        public string SenderId { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
