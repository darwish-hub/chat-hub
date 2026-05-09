using System.Text.Json;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Nats;

/// <summary>
/// Background service that subscribes to NATS and fans out messages to local connections
/// </summary>
public class NatsSubscriberService : BackgroundService
{
    private readonly INatsBackplane _backplane;
    private readonly IWebSocketSender _webSocketSender;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly ChatHubSettings _settings;
    private readonly ILogger<NatsSubscriberService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public NatsSubscriberService(
        INatsBackplane backplane,
        IWebSocketSender webSocketSender,
        IConnectionRegistry connectionRegistry,
        IOptions<ChatHubSettings> settings,
        ILogger<NatsSubscriberService> logger)
    {
        _backplane = backplane;
        _webSocketSender = webSocketSender;
        _connectionRegistry = connectionRegistry;
        _settings = settings.Value;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to messages with queue group (load balanced)
        await _backplane.SubscribeAsync(
            "chathub.*.messages",
            "chathub-hub",
            OnMessageReceived,
            stoppingToken);
        
        // Subscribe to presence events with queue group
        await _backplane.SubscribeAsync(
            "chathub.*.presence",
            "chathub-hub",
            OnPresenceReceived,
            stoppingToken);
        
        // Subscribe to system broadcasts without queue group (all pods receive)
        await _backplane.SubscribeAsync(
            "chathub.system.broadcast",
            null,
            OnBroadcastReceived,
            stoppingToken);
        
        _logger.LogInformation("NATS subscriber started with pod ID {PodId}", _settings.PodId);
        
        // Keep running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }
    
    private async Task OnMessageReceived(string subject, ReadOnlyMemory<byte> payload)
    {
        // Extract serviceId from subject (chathub.{serviceId}.messages)
        var parts = subject.Split('.');
        if (parts.Length < 3) return;
        
        var serviceId = parts[1];
        
        // Fan out to all local connections in this service
        await _webSocketSender.BroadcastToServiceAsync(serviceId, payload, CancellationToken.None)
            .ConfigureAwait(false);
        
        _logger.LogDebug("Fanned out message to service {ServiceId}", serviceId);
    }
    
    private async Task OnPresenceReceived(string subject, ReadOnlyMemory<byte> payload)
    {
        // Extract serviceId from subject
        var parts = subject.Split('.');
        if (parts.Length < 3) return;
        
        var serviceId = parts[1];
        
        // Fan out presence events to local connections
        await _webSocketSender.BroadcastToServiceAsync(serviceId, payload, CancellationToken.None)
            .ConfigureAwait(false);
    }
    
    private async Task OnBroadcastReceived(string subject, ReadOnlyMemory<byte> payload)
    {
        // Broadcast to ALL local connections across all services
        foreach (var serviceId in _connectionRegistry.ServiceIndex.Keys)
        {
            await _webSocketSender.BroadcastToServiceAsync(serviceId, payload, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
