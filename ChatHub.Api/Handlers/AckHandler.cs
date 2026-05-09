using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles message acknowledgments
/// </summary>
public class AckHandler : IMessageHandler<AckMessage>
{
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<AckHandler> _logger;
    
    public AckHandler(
        IMessageRepository messageRepository,
        ILogger<AckHandler> logger)
    {
        _messageRepository = messageRepository;
        _logger = logger;
    }
    
    public async Task HandleAsync(string connectionId, AckMessage message, CancellationToken ct)
    {
        await _messageRepository.MarkDeliveredAsync(message.MessageId, ct);
        
        _logger.LogDebug(
            "Message {MessageId} marked as delivered",
            message.MessageId);
    }
}
