using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ChatHub.Api.Handlers;

public class PongHandler : IPongHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly ILogger<PongHandler> _logger;

    public PongHandler(
        IConnectionRegistry connectionRegistry,
        ILogger<PongHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _logger = logger;
    }

    public Task HandleAsync(string connectionId, PongMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection != null)
        {
            connection.LastPongAt = DateTime.UtcNow;
            _logger.LogDebug("Received pong from connection {ConnectionId}", connectionId);
        }

        return Task.CompletedTask;
    }
}
