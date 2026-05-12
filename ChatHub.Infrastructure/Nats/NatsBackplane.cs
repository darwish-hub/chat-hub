using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;

namespace ChatHub.Infrastructure.Nats;

public class NatsBackplane : INatsBackplane, IDisposable
{
    private readonly IConnection _connection;
    private readonly string _podId;
    private readonly ILogger<NatsBackplane> _logger;

    public NatsBackplane(NatsSettings settings, string podId, ILogger<NatsBackplane> logger)
    {
        _podId = podId;
        _logger = logger;
        var options = ConnectionFactory.GetDefaultOptions();
        options.Url = settings.Url;
        _connection = new ConnectionFactory().CreateConnection(options);
        _logger.LogInformation("NatsBackplane: Connected to NATS at {Url}, podId={PodId}", settings.Url, podId);
    }

    public Task PublishAsync(string subject, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        try
        {
            var headers = new MsgHeader();
            headers.Add("source-pod", _podId);

            var msg = new Msg(subject, null, headers, payload.ToArray());
            _connection.Publish(msg);
            _logger.LogDebug("NatsBackplane: Published to {Subject}, {ByteCount} bytes, source-pod={PodId}", subject, payload.Length, _podId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NatsBackplane: Publish failed for subject {Subject} - {ErrorMessage}", subject, ex.Message);
            throw;
        }
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string subject, string? queueGroup, Func<string, ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default)
    {
        _logger.LogDebug("NatsBackplane: Subscribing to {Subject} with queueGroup={QueueGroup}", subject, queueGroup ?? "none");
        EventHandler<MsgHandlerEventArgs> msgHandler = async (sender, args) =>
        {
            // Skip messages from this pod (already handled locally)
            if (args.Message.Header != null &&
                args.Message.Header["source-pod"] == _podId)
            {
                _logger.LogDebug("NatsBackplane: Skipping message from self on {Subject}", args.Message.Subject);
                return;
            }

            _logger.LogDebug("NatsBackplane: Received message on {Subject}, {ByteCount} bytes", args.Message.Subject, args.Message.Data.Length);
            await handler(args.Message.Subject, args.Message.Data);
        };

        if (!string.IsNullOrEmpty(queueGroup))
        {
            _connection.SubscribeAsync(subject, queueGroup, msgHandler);
            _logger.LogDebug("NatsBackplane: Subscribed to {Subject} with queue group {QueueGroup}", subject, queueGroup);
        }
        else
        {
            _connection.SubscribeAsync(subject, msgHandler);
            _logger.LogDebug("NatsBackplane: Subscribed to {Subject} without queue group", subject);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _logger.LogDebug("NatsBackplane: Disposing connection");
        _connection?.Dispose();
    }
}
