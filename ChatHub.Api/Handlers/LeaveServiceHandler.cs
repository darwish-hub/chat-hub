using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

public class LeaveServiceHandler : ILeaveServiceHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IWebSocketSender _webSocketSender;
    private readonly INatsBackplane _natsBackplane;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<LeaveServiceHandler> _logger;

    public LeaveServiceHandler(
        IConnectionRegistry connectionRegistry,
        IWebSocketSender webSocketSender,
        INatsBackplane natsBackplane,
        IPresenceService presenceService,
        ILogger<LeaveServiceHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _webSocketSender = webSocketSender;
        _natsBackplane = natsBackplane;
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, LeaveServiceMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for leave service", connectionId);
            return;
        }

        _connectionRegistry.LeaveService(connectionId, message.ServiceId);

        if (connection.CurrentServiceId == message.ServiceId)
        {
            connection.CurrentServiceId = null;
        }

        _logger.LogInformation("User {UserId} left service {ServiceId} via connection {ConnectionId}",
            connection.UserId, message.ServiceId, connectionId);

        // Remove presence tracking
        await _presenceService.SetUserOfflineAsync(message.ServiceId, connection.UserId, ct);

        // Broadcast user left to other participants
        var userLeftMessage = new UserLeft
        {
            UserId = connection.UserId,
            ServiceId = message.ServiceId
        };

        var json = JsonSerializer.Serialize(userLeftMessage, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Send to all connections in the service on this pod
        await _webSocketSender.BroadcastToServiceAsync(message.ServiceId, bytes, ct);

        // Publish to NATS for cross-pod broadcast
        var subject = $"chathub.{message.ServiceId}.presence";
        await _natsBackplane.PublishAsync(subject, bytes, ct);
    }
}
