using System.Net.WebSockets;
using System.Text.Json;
using ChatHub.Core.Interfaces;

namespace ChatHub.Infrastructure.WebSockets;

/// <summary>
/// Sends messages to WebSocket connections via their send queues
/// </summary>
public class WebSocketSender : IWebSocketSender
{
    private readonly IConnectionRegistry _registry;
    private readonly ILogger<WebSocketSender> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public WebSocketSender(
        IConnectionRegistry registry,
        ILogger<WebSocketSender> logger)
    {
        _registry = registry;
        _logger = logger;
    }
    
    public Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        if (_registry.GetConnection(connectionId) is WebSocketConnection connection)
        {
            connection.QueueSend(utf8Json, WebSocketMessageType.Text);
            return Task.CompletedTask;
        }
        
        _logger.LogWarning("Connection {ConnectionId} not found for send", connectionId);
        return Task.CompletedTask;
    }
    
    public Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_registry.GetConnection(connectionId) is WebSocketConnection connection)
        {
            connection.QueueSend(data, WebSocketMessageType.Binary);
            return Task.CompletedTask;
        }
        
        _logger.LogWarning("Connection {ConnectionId} not found for binary send", connectionId);
        return Task.CompletedTask;
    }
    
    public async Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var connections = _registry.GetConnectionsByService(serviceId);
        var tasks = new List<Task>();
        
        foreach (var conn in connections)
        {
            if (conn is WebSocketConnection connection)
            {
                connection.QueueSend(utf8Json, WebSocketMessageType.Text);
            }
        }
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
    
    public async Task SendToUserAsync(string userId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var connections = _registry.GetConnectionsByUser(userId);
        var tasks = new List<Task>();
        
        foreach (var conn in connections)
        {
            if (conn is WebSocketConnection connection)
            {
                connection.QueueSend(utf8Json, WebSocketMessageType.Text);
            }
        }
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
