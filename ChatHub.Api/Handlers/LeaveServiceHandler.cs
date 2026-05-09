using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles leave_service messages
/// </summary>
public class LeaveServiceHandler : IMessageHandler<LeaveServiceMessage>
{
    private readonly IConnectionRegistry _registry;
    private readonly INatsBackplane _nats;
    private readonly IWebSocketSender _sender;
    private readonly ILogger<LeaveServiceHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public LeaveServiceHandler(
        IConnectionRegistry registry,
        INatsBackplane nats,
        IWebSocketSender sender,
        ILogger<LeaveServiceHandler> logger)
    {
        _registry = registry;
        _nats = nats;
        _sender = sender;
        _logger = logger;
    }
    
    public async Task HandleAsync(string connectionId, LeaveServiceMessage message, CancellationToken ct)
    {
        var connection = _registry.GetConnection(connectionId);
        if (connection == null) return;
        
        _registry.RemoveFromService(connectionId, message.ServiceId);
        
        // Publish presence to NATS
        var presenceEvent = new
        {
            eventType = "left",
            userId = connection.UserId,
            connectionId,
            serviceId = message.ServiceId
        };
        
        var payload = JsonSerializer.SerializeToUtf8Bytes(presenceEvent, JsonOptions);
        await _nats.PublishAsync($"chathub.{message.ServiceId}.presence", payload, ct);
        
        // Notify other connections
        var userLeft = new UserLeft
        {
            UserId = connection.UserId,
            ServiceId = message.ServiceId
        };
        
        var notification = JsonSerializer.SerializeToUtf8Bytes(userLeft, JsonOptions);
        await _sender.BroadcastToServiceAsync(message.ServiceId, notification, ct);
        
        _logger.LogInformation(
            "User {UserId} left service {ServiceId}",
            connection.UserId, message.ServiceId);
    }
}
