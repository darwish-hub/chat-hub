using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles join_service messages
/// </summary>
public class JoinServiceHandler : IMessageHandler<JoinServiceMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly INatsBackplane _nats;
    private readonly IWebSocketSender _sender;
    private readonly ILogger<JoinServiceHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public JoinServiceHandler(
        IConnectionRegistry registry,
        INatsBackplane nats,
        IWebSocketSender sender,
        ILogger<JoinServiceHandler> logger)
    {
        _registry = registry;
        _nats = nats;
        _sender = sender;
        _logger = logger;
    }
    
    public async Task HandleAsync(string connectionId, JoinServiceMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        _registry.AddToService(connectionId, message.ServiceId);
        
        // Publish presence to NATS
        var presenceEvent = new
        {
            eventType = "joined",
            userId = connection.UserId,
            connectionId,
            serviceId = message.ServiceId
        };
        
        var payload = JsonSerializer.SerializeToUtf8Bytes(presenceEvent, JsonOptions);
        await _nats.PublishAsync($"chathub.{message.ServiceId}.presence", payload, ct);
        
        // Notify other connections in the same service on this pod
        var userJoined = new UserJoined
        {
            UserId = connection.UserId,
            ServiceId = message.ServiceId,
            DisplayName = connection.UserId // Could be enhanced with actual display name
        };
        
        var notification = JsonSerializer.SerializeToUtf8Bytes(userJoined, JsonOptions);
        await _sender.BroadcastToServiceAsync(message.ServiceId, notification, ct);
        
        _logger.LogInformation(
            "User {UserId} joined service {ServiceId}",
            connection.UserId, message.ServiceId);
    }
}
