using ChatHub.Core.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ChatHub.Infrastructure.WebSockets;

public class WebSocketSender : IWebSocketSender, IDisposable
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly ConcurrentDictionary<string, Channel<SendItem>> _sendChannels = new();
    private readonly ConcurrentDictionary<string, Task> _sendLoops = new();

    public WebSocketSender(IConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public async Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var channel = GetOrCreateSendChannel(connectionId);
        await channel.Writer.WriteAsync(new SendItem(SendItemType.Text, utf8Json), ct);
    }

    public async Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var channel = GetOrCreateSendChannel(connectionId);
        await channel.Writer.WriteAsync(new SendItem(SendItemType.Binary, data), ct);
    }

    public async Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
    {
        var connectionIds = _connectionRegistry.GetServiceConnectionIds(serviceId);
        var tasks = connectionIds.Select(id => SendTextAsync(id, utf8Json, ct));
        await Task.WhenAll(tasks);
    }

    private Channel<SendItem> GetOrCreateSendChannel(string connectionId)
    {
        return _sendChannels.GetOrAdd(connectionId, _ =>
        {
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
        try
        {
            await foreach (var item in reader.ReadAllAsync())
            {
                var connection = _connectionRegistry.Get(connectionId);
                if (connection == null || connection.WebSocket.State != System.Net.WebSockets.WebSocketState.Open)
                    continue;

                try
                {
                    var segment = new ArraySegment<byte>(item.Data.ToArray());
                    var messageType = item.ItemType == SendItemType.Text
                        ? System.Net.WebSockets.WebSocketMessageType.Text
                        : System.Net.WebSockets.WebSocketMessageType.Binary;

                    await connection.WebSocket.SendAsync(
                        segment,
                        messageType,
                        endOfMessage: true,
                        connection.ConnectionToken);
                }
                catch (Exception)
                {
                    // Connection likely closed, will be cleaned up elsewhere
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Channel closed or other error
        }
        finally
        {
            _sendChannels.TryRemove(connectionId, out _);
            _sendLoops.TryRemove(connectionId, out _);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (_sendChannels.TryRemove(connectionId, out var channel))
        {
            channel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        foreach (var channel in _sendChannels.Values)
        {
            channel.Writer.Complete();
        }
        _sendChannels.Clear();
    }

    private record SendItem(SendItemType ItemType, ReadOnlyMemory<byte> Data);
    private enum SendItemType { Text, Binary }
}
