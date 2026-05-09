using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Text.Json;

namespace ChatHub.Api.Handlers;

public class DeliveredHandler : IAckHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<DeliveredHandler> _logger;

    public DeliveredHandler(
        IConnectionRegistry connectionRegistry,
        IMessageRepository messageRepository,
        ILogger<DeliveredHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, AckMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for ack", connectionId);
            return;
        }

        // Update delivered timestamp
        await _messageRepository.UpdateDeliveredAtAsync(message.MessageId, DateTime.UtcNow, ct);

        _logger.LogDebug("Message {MessageId} marked as delivered by user {UserId}",
            message.MessageId, connection.UserId);
    }
}
