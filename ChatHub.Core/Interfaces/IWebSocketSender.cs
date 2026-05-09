namespace ChatHub.Core.Interfaces;

/// <summary>
/// Sends messages to WebSocket connections
/// </summary>
public interface IWebSocketSender
{
    /// <summary>
    /// Send a text message to a specific connection
    /// </summary>
    Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
    
    /// <summary>
    /// Send binary data to a specific connection
    /// </summary>
    Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct);
    
    /// <summary>
    /// Broadcast a message to all connections in a service
    /// </summary>
    Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
    
    /// <summary>
    /// Send a message to all connections of a specific user
    /// </summary>
    Task SendToUserAsync(string userId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
}
