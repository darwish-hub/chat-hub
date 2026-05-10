using System.Net.WebSockets;

namespace ChatHub.Core.Interfaces;

public interface IWebSocketConnection
{
    string ConnectionId { get; }
    string UserId { get; }
    System.Security.Claims.ClaimsPrincipal User { get; }
    WebSocket WebSocket { get; }
    DateTime ConnectedAt { get; }
    DateTime LastPongAt { get; }
    string? CurrentServiceId { get; set; }
    CancellationToken ConnectionToken { get; }
    CancellationTokenSource Cts { get; }

    void UpdateLastPong();
    void JoinService(string serviceId);
    void LeaveService(string serviceId);
    void QueueSend(ReadOnlyMemory<byte> data, WebSocketMessageType type);
    void Abort();
    Task CloseAsync(WebSocketCloseStatus status, string description);
}
