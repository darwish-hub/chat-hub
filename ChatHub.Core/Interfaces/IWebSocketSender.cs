namespace ChatHub.Core.Interfaces;

public interface IWebSocketSender
{
    Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
    Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct);
    Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
}
