using ChatHub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ChatHub.Infrastructure.WebSockets;

public class WebSocketSender : IWebSocketSender, IDisposable
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly ConcurrentDictionary<string, Channel<SendItem>> _sendChannels = new();
    private readonly ConcurrentDictionary<string, Task> _sendLoops = new();
    private readonly ILogger<WebSocketSender> _logger;

    public WebSocketSender(IConnectionRegistry connectionRegistry, ILogger<WebSocketSender> logger)
    {
        _connectionRegistry = connectionRegistry;
        _logger = logger;
    }

    public async Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var channel = GetOrCreateSendChannel(connectionId);
        _logger.LogDebug("WebSocketSender: WriteAsync to channel for connection {ConnectionId}, {ByteCount} bytes", connectionId, utf8Json.Length);
        var success = await channel.Writer.WaitToWriteAsync(ct);
        if (!success)
        {
            _logger.LogWarning("WebSocketSender: Channel closed for connection {ConnectionId}", connectionId);
            return;
        }
        await channel.Writer.WriteAsync(new SendItem(SendItemType.Text, utf8Json), ct);
        _logger.LogDebug("WebSocketSender: Written to channel for connection {ConnectionId}", connectionId);
    }

    public async Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var channel = GetOrCreateSendChannel(connectionId);
        _logger.LogDebug("WebSocketSender: WriteAsync (binary) to channel for connection {ConnectionId}, {ByteCount} bytes", connectionId, data.Length);
        var success = await channel.Writer.WaitToWriteAsync(ct);
        if (!success)
        {
            _logger.LogWarning("WebSocketSender: Channel closed for connection {ConnectionId}", connectionId);
            return;
        }
        await channel.Writer.WriteAsync(new SendItem(SendItemType.Binary, data), ct);
    }

    public async Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var connectionIds = _connectionRegistry.GetServiceConnectionIds(serviceId).ToList();
        _logger.LogDebug("WebSocketSender: BroadcastToService {ServiceId} to {ConnectionCount} connections", serviceId, connectionIds.Count);
        var tasks = connectionIds.Select(id => SendTextAsync(id, utf8Json, ct));
        await Task.WhenAll(tasks);
    }

    private Channel<SendItem> GetOrCreateSendChannel(string connectionId)
    {
        return _sendChannels.GetOrAdd(connectionId, _ =>
        {
            _logger.LogDebug("WebSocketSender: Creating new send channel for connection {ConnectionId}", connectionId);
            var channel = Channel.CreateUnbounded<SendItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            // Start send loop for this connection
            var loopTask = Task.Run(() => SendLoopAsync(connectionId, channel.Reader));
            _sendLoops[connectionId] = loopTask;

            return channel;
        });
    }

    private async Task SendLoopAsync(string connectionId, ChannelReader<SendItem> reader)
    {
        _logger.LogDebug("WebSocketSender: SendLoop started for connection {ConnectionId}", connectionId);
        try
        {
            await foreach (var item in reader.ReadAllAsync())
            {
                _logger.LogDebug("WebSocketSender: SendLoop processing item for connection {ConnectionId}, {ByteCount} bytes", connectionId, item.Data.Length);
                var connection = _connectionRegistry.Get(connectionId);
                if (connection == null)
                {
                    _logger.LogDebug("WebSocketSender: Connection {ConnectionId} not found in registry", connectionId);
                    continue;
                }
                if (connection.WebSocket.State != System.Net.WebSockets.WebSocketState.Open)
                {
                    _logger.LogDebug("WebSocketSender: Connection {ConnectionId} WebSocket not open, state={State}", connectionId, connection.WebSocket.State);
                    continue;
                }

                try
                {
                    var segment = new ArraySegment<byte>(item.Data.ToArray());
                    var messageType = item.ItemType == SendItemType.Text
                        ? System.Net.WebSockets.WebSocketMessageType.Text
                        : System.Net.WebSockets.WebSocketMessageType.Binary;

                    _logger.LogDebug("WebSocketSender: Sending {MessageType} to connection {ConnectionId}", messageType, connectionId);
                    await connection.WebSocket.SendAsync(
                        segment,
                        messageType,
                        endOfMessage: true,
                        connection.ConnectionToken);
                    _logger.LogDebug("WebSocketSender: Successfully sent to connection {ConnectionId}", connectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WebSocketSender: Send failed for connection {ConnectionId} - {ErrorMessage}", connectionId, ex.Message);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocketSender: SendLoop error for connection {ConnectionId} - {ErrorMessage}", connectionId, ex.Message);
        }
        finally
        {
            _logger.LogDebug("WebSocketSender: SendLoop exiting for connection {ConnectionId}", connectionId);
            _sendChannels.TryRemove(connectionId, out _);
            _sendLoops.TryRemove(connectionId, out _);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (_sendChannels.TryRemove(connectionId, out var channel))
        {
            _logger.LogDebug("WebSocketSender: RemoveConnection completing channel for {ConnectionId}", connectionId);
            channel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        _logger.LogDebug("WebSocketSender: Dispose called");
        foreach (var channel in _sendChannels.Values)
        {
            channel.Writer.Complete();
        }
        _sendChannels.Clear();
    }

    private record SendItem(SendItemType ItemType, ReadOnlyMemory<byte> Data);
    private enum SendItemType { Text, Binary }
}
