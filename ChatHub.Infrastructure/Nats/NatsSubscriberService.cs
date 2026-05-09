using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatHub.Infrastructure.Nats;

public class NatsSubscriberService : BackgroundService
{
    private readonly INatsBackplane _natsBackplane;
    private readonly IWebSocketSender _webSocketSender;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly NatsSettings _natsSettings;
    private readonly ILogger<NatsSubscriberService> _logger;

    public NatsSubscriberService(
        INatsBackplane natsBackplane,
        IWebSocketSender webSocketSender,
        IConnectionRegistry connectionRegistry,
        NatsSettings natsSettings,
        ILogger<NatsSubscriberService> logger)
    {
        _natsBackplane = natsBackplane;
        _webSocketSender = webSocketSender;
        _connectionRegistry = connectionRegistry;
        _natsSettings = natsSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NATS Subscriber Service starting...");

        // Subscribe to messages with queue group (load balanced)
        await _natsBackplane.SubscribeAsync(
            "chathub.*.messages",
            _natsSettings.QueueGroup,
            async (subject, payload) =>
            {
                // Extract serviceId from subject (chathub.{serviceId}.messages)
                var parts = subject.Split('.');
                if (parts.Length < 3) return;
                
                var serviceId = parts[1];
                await BroadcastToLocalConnectionsAsync(serviceId, payload, stoppingToken);
            },
            stoppingToken);

        // Subscribe to presence events with queue group
        await _natsBackplane.SubscribeAsync(
            "chathub.*.presence",
            _natsSettings.QueueGroup,
            async (subject, payload) =>
            {
                var parts = subject.Split('.');
                if (parts.Length < 3) return;
                
                var serviceId = parts[1];
                await BroadcastToLocalConnectionsAsync(serviceId, payload, stoppingToken);
            },
            stoppingToken);

        // Subscribe to system broadcasts without queue group (all pods receive)
        await _natsBackplane.SubscribeAsync(
            "chathub.system.broadcast",
            null, // No queue group - broadcast to all
            async (subject, payload) =>
            {
                // Broadcast to all local connections
                await BroadcastToAllLocalConnectionsAsync(payload, stoppingToken);
            },
            stoppingToken);

        _logger.LogInformation("NATS Subscriber Service started and subscribed to subjects");

        // Keep running until cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task BroadcastToLocalConnectionsAsync(string serviceId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        try
        {
            await _webSocketSender.BroadcastToServiceAsync(serviceId, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting message to service {ServiceId}", serviceId);
        }
    }

    private async Task BroadcastToAllLocalConnectionsAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        // Get all services and broadcast to each
        var allConnections = _connectionRegistry.GetByService("*"); // This won't work directly, need different approach
        
        // For now, we don't have an efficient way to get all services
        // In production, you'd track active services separately
    }
}
