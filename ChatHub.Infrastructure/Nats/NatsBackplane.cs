using NATS.Client;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Nats;

/// <summary>
/// NATS backplane implementation for cross-pod messaging
/// </summary>
public class NatsBackplane : INatsBackplane, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly ILogger<NatsBackplane> _logger;
    private readonly List<IAsyncSubscription> _subscriptions = new();
    
    public NatsBackplane(
        IOptions<NatsSettings> settings,
        ILogger<NatsBackplane> logger)
    {
        _logger = logger;
        
        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = settings.Value.Url;
        opts.MaxReconnect = Options.ReconnectForever;
        opts.ReconnectWait = 1000;
        
        var factory = new ConnectionFactory();
        _connection = factory.CreateConnection(opts);
        
        _logger.LogInformation("Connected to NATS at {Url}", settings.Value.Url);
    }
    
    public Task PublishAsync(string subject, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        try
        {
            _connection.Publish(subject, payload.ToArray());
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to NATS subject {Subject}", subject);
            throw;
        }
    }
    
    public Task SubscribeAsync(
        string subject,
        string? queueGroup,
        Func<string, ReadOnlyMemory<byte>, Task> handler,
        CancellationToken ct)
    {
        IAsyncSubscription subscription;
        
        if (!string.IsNullOrEmpty(queueGroup))
        {
            subscription = _connection.SubscribeAsync(subject, queueGroup, (sender, args) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler(args.Message.Subject, args.Message.Data).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling NATS message on subject {Subject}", args.Message.Subject);
                    }
                }, ct);
            });
        }
        else
        {
            subscription = _connection.SubscribeAsync(subject, (sender, args) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler(args.Message.Subject, args.Message.Data).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling NATS message on subject {Subject}", args.Message.Subject);
                    }
                }, ct);
            });
        }
        
        _subscriptions.Add(subscription);
        _logger.LogInformation("Subscribed to NATS subject {Subject} with queue group {QueueGroup}", 
            subject, queueGroup ?? "(none)");
        
        return Task.CompletedTask;
    }
    
    public ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Unsubscribe();
        }
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }
}
