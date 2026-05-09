using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading.Channels;
using ChatHub.Core.Interfaces;

namespace ChatHub.Infrastructure.WebSockets;

/// <summary>
/// Active WebSocket connection with send queue and metadata
/// </summary>
public class WebSocketConnection : IWebSocketConnection, IDisposable
{
    private readonly Channel<SendItem> _sendChannel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _sendTask;
    
    public string ConnectionId { get; }
    public string UserId { get; }
    public ClaimsPrincipal User { get; }
    public WebSocket WebSocket { get; }
    public DateTime ConnectedAt { get; }
    public DateTime LastPongAt { get; private set; }
    public HashSet<string> JoinedServices { get; } = new();
    public CancellationToken ConnectionToken => _cts.Token;
    
    public WebSocketConnection(
        string connectionId,
        string userId,
        ClaimsPrincipal user,
        WebSocket webSocket)
    {
        ConnectionId = connectionId;
        UserId = userId;
        User = user;
        WebSocket = webSocket;
        ConnectedAt = DateTime.UtcNow;
        LastPongAt = DateTime.UtcNow;
        
        _sendChannel = Channel.CreateUnbounded<SendItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        _cts = new CancellationTokenSource();
        _sendTask = RunSendLoopAsync();
    }
    
    public void UpdateLastPong()
    {
        LastPongAt = DateTime.UtcNow;
    }
    
    public void JoinService(string serviceId)
    {
        lock (JoinedServices)
        {
            JoinedServices.Add(serviceId);
        }
    }
    
    public void LeaveService(string serviceId)
    {
        lock (JoinedServices)
        {
            JoinedServices.Remove(serviceId);
        }
    }
    
    public void QueueSend(ReadOnlyMemory<byte> data, WebSocketMessageType type)
    {
        _sendChannel.Writer.TryWrite(new SendItem(data, type));
    }
    
    public void Abort()
    {
        _cts.Cancel();
        try
        {
            WebSocket.Abort();
        }
        catch { /* ignored */ }
    }
    
    public async Task CloseAsync(WebSocketCloseStatus status, string description)
    {
        _sendChannel.Writer.Complete();
        _cts.CancelAfter(TimeSpan.FromSeconds(5));
        
        try
        {
            await _sendTask.ConfigureAwait(false);
        }
        catch { /* ignored */ }
        
        if (WebSocket.State == WebSocketState.Open)
        {
            try
            {
                await WebSocket.CloseAsync(status, description, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch { /* ignored */ }
        }
    }
    
    private async Task RunSendLoopAsync()
    {
        await foreach (var item in _sendChannel.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                if (WebSocket.State == WebSocketState.Open)
                {
                    await WebSocket.SendAsync(
                        item.Data,
                        item.MessageType,
                        endOfMessage: true,
                        _cts.Token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Send failed, connection likely closing
                break;
            }
        }
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        WebSocket.Dispose();
    }
    
    private record SendItem(ReadOnlyMemory<byte> Data, WebSocketMessageType MessageType);
}
