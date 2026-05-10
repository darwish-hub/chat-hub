using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

public class JoinServiceHandler : IJoinServiceHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IWebSocketSender _webSocketSender;
    private readonly INatsBackplane _natsBackplane;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<JoinServiceHandler> _logger;

    public JoinServiceHandler(
        IConnectionRegistry connectionRegistry,
        IWebSocketSender webSocketSender,
        INatsBackplane natsBackplane,
        IPresenceService presenceService,
        ILogger<JoinServiceHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _webSocketSender = webSocketSender;
        _natsBackplane = natsBackplane;
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, JoinServiceMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for join service", connectionId);
            return;
        }

        _connectionRegistry.JoinService(connectionId, message.ServiceId);
        connection.CurrentServiceId = message.ServiceId;

        _logger.LogInformation("User {UserId} joined service {ServiceId} via connection {ConnectionId}",
            connection.UserId, message.ServiceId, connectionId);

        // Track presence in memory
        await _presenceService.SetUserOnlineAsync(message.ServiceId, connection.UserId, connectionId, ct);

        // Broadcast user joined to other participants in the service
        var userJoinedMessage = new UserJoined
        {
            UserId = connection.UserId,
            ServiceId = message.ServiceId,
            DisplayName = connection.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? connection.UserId
        };

        var json = JsonSerializer.Serialize(userJoinedMessage, new JsonSerializerOptions
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
