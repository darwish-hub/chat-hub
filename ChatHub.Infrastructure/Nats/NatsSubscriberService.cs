using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
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
    private readonly IConversationRepository _conversationRepository;
    private readonly NatsSettings _natsSettings;
    private readonly ILogger<NatsSubscriberService> _logger;

    public NatsSubscriberService(
        INatsBackplane natsBackplane,
        IWebSocketSender webSocketSender,
        IConnectionRegistry connectionRegistry,
        IConversationRepository conversationRepository,
        NatsSettings natsSettings,
        ILogger<NatsSubscriberService> logger)
    {
        _natsBackplane = natsBackplane;
        _webSocketSender = webSocketSender;
        _connectionRegistry = connectionRegistry;
        _conversationRepository = conversationRepository;
        _natsSettings = natsSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NATS Subscriber Service starting...");

        await _natsBackplane.SubscribeAsync(
            "chathub.*.messages",
            _natsSettings.QueueGroup,
            async (subject, payload) =>
            {
                var parts = subject.Split('.');
                if (parts.Length < 3) return;

                var serviceId = parts[1];
                await HandleCrossPodMessageAsync(serviceId, payload, stoppingToken);
            },
            stoppingToken);

        await _natsBackplane.SubscribeAsync(
            "chathub.*.presence",
            _natsSettings.QueueGroup,
            async (subject, payload) =>
            {
                var parts = subject.Split('.');
                if (parts.Length < 3) return;

                var serviceId = parts[1];
                await BroadcastToLocalServiceAsync(serviceId, payload, stoppingToken);
            },
            stoppingToken);

        await _natsBackplane.SubscribeAsync(
            "chathub.system.broadcast",
            null,
            async (subject, payload) =>
            {
                await BroadcastToAllLocalConnectionsAsync(payload, stoppingToken);
            },
            stoppingToken);

        _logger.LogInformation("NATS Subscriber Service started and subscribed to subjects");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCrossPodMessageAsync(string serviceId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        _logger.LogDebug("NatsSubscriber: HandleCrossPodMessageAsync called for service {ServiceId}, {ByteCount} bytes", serviceId, payload.Length);
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(payload.Span);
            var messageReceived = JsonSerializer.Deserialize<MessageReceived>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (messageReceived?.Envelope == null)
            {
                _logger.LogWarning("NatsSubscriber: Failed to deserialize cross-pod message on subject chathub.{ServiceId}.messages", serviceId);
                await BroadcastToLocalServiceAsync(serviceId, payload, ct);
                return;
            }

            var envelope = messageReceived.Envelope;
            _logger.LogDebug("NatsSubscriber: Deserialized message {MessageId} from {SenderId} to conversation {ConversationId}",
                envelope.Id, envelope.SenderId, envelope.ConversationId);

            var conversation = await _conversationRepository.GetByIdAsync(envelope.ConversationId, ct);

            if (conversation != null)
            {
                _logger.LogDebug("NatsSubscriber: Found conversation {ConversationId} with {ParticipantCount} participants",
                    conversation.Id, conversation.ParticipantIds.Count);
                foreach (var participantId in conversation.ParticipantIds)
                {
                    if (participantId == envelope.SenderId)
                    {
                        _logger.LogDebug("NatsSubscriber: Skipping sender {SenderId}", envelope.SenderId);
                        continue;
                    }

                    _logger.LogDebug("NatsSubscriber: Looking up connections for participant {ParticipantId}", participantId);
                    var participantConnections = _connectionRegistry.GetByUser(participantId).ToList();
                    _logger.LogDebug("NatsSubscriber: Found {ConnectionCount} connections for participant {ParticipantId}",
                        participantConnections.Count, participantId);

                    foreach (var conn in participantConnections)
                    {
                        _logger.LogDebug("NatsSubscriber: Sending cross-pod message {MessageId} to connection {ConnectionId}",
                            envelope.Id, conn.ConnectionId);
                        await _webSocketSender.SendTextAsync(conn.ConnectionId, payload, ct);
                        _logger.LogDebug("NatsSubscriber: Cross-pod message {MessageId} sent to connection {ConnectionId}",
                            envelope.Id, conn.ConnectionId);
                    }
                }
            }
            else
            {
                _logger.LogWarning("NatsSubscriber: Conversation {ConversationId} not found, falling back to BroadcastToLocalService",
                    envelope.ConversationId);
                await BroadcastToLocalServiceAsync(serviceId, payload, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NatsSubscriber: Error handling cross-pod message for service {ServiceId} - {ErrorMessage}", serviceId, ex.Message);
            await BroadcastToLocalServiceAsync(serviceId, payload, ct);
        }
    }

    private async Task BroadcastToLocalServiceAsync(string serviceId, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("NatsSubscriber: BroadcastToLocalService {ServiceId}, {ByteCount} bytes", serviceId, payload.Length);
            await _webSocketSender.BroadcastToServiceAsync(serviceId, payload, ct);
            _logger.LogDebug("NatsSubscriber: BroadcastToLocalService completed for {ServiceId}", serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NatsSubscriber: Error broadcasting message to service {ServiceId} - {ErrorMessage}", serviceId, ex.Message);
        }
    }

    private async Task BroadcastToAllLocalConnectionsAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        _logger.LogDebug("NatsSubscriber: BroadcastToAllLocalConnections called");
    }
}