using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles voice chunk messages (live streaming)
/// </summary>
public class VoiceChunkHandler : IMessageHandler<VoiceChunkMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly IVoiceSessionBuffer _voiceBuffer;
    private readonly IWebSocketSender _sender;
    private readonly ILogger<VoiceChunkHandler> _logger;
    
    public VoiceChunkHandler(
        IConnectionRegistry registry,
        IVoiceSessionBuffer voiceBuffer,
        IWebSocketSender sender,
        ILogger<VoiceChunkHandler> logger)
    {
        _registry = registry;
        _voiceBuffer = voiceBuffer;
        _sender = sender;
        _logger = logger;
    }
    
    public async Task HandleAsync(string connectionId, VoiceChunkMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        // Store chunk in Redis
        // Note: Actual audio data comes in a separate binary frame handled by middleware
        // Here we just track the metadata
        
        if (message.IsFinal)
        {
            // Assembly would happen when binary frames are collected
            _logger.LogInformation(
                "Final voice chunk received for message {MessageId}",
                message.Id);
        }
        
        // Forward to other connections in the conversation
        var connections = _registry.GetConnectionsByService(connection.JoinedServices.FirstOrDefault() ?? "");
        foreach (var conn in connections.Where(c => c.ConnectionId != connectionId))
        {
            // Would queue binary data here
        }
        
        _logger.LogDebug(
            "Voice chunk {SequenceNumber} for message {MessageId} from user {UserId}",
            message.SequenceNumber, message.Id, connection.UserId);
    }
}
