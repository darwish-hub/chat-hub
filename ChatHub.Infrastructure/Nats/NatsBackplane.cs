using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using NATS.Client;
using System.Text;

namespace ChatHub.Infrastructure.Nats;

public class NatsBackplane : INatsBackplane, IDisposable
{
    private readonly IConnection _connection;
    private readonly string _podId;

    public NatsBackplane(NatsSettings settings, string podId)
    {
        _podId = podId;
        var options = ConnectionFactory.GetDefaultOptions();
        options.Url = settings.Url;
        _connection = new ConnectionFactory().CreateConnection(options);
    }

    public Task PublishAsync(string subject, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var headers = new MsgHeader();
        headers.Add("source-pod", _podId);
        
        var msg = new Msg(subject, null, headers, payload.ToArray());
        _connection.Publish(msg);
        
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string subject, string? queueGroup, Func<string, ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default)
    {
        EventHandler<MsgHandlerEventArgs> msgHandler = async (sender, args) =>
        {
            // Skip messages from this pod (already handled locally)
            if (args.Message.Header != null &&
                args.Message.Header["source-pod"] == _podId)
            {
                return;
            }

            await handler(args.Message.Subject, args.Message.Data);
        };

        if (!string.IsNullOrEmpty(queueGroup))
        {
            _connection.SubscribeAsync(subject, queueGroup, msgHandler);
        }
        else
        {
            _connection.SubscribeAsync(subject, msgHandler);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
