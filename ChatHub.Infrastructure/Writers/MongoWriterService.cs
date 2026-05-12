using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ChatHub.Infrastructure.Writers;

public class MongoWriterService : BackgroundService
{
    private readonly Channel<MessageDocument> _messageChannel;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<MongoWriterService> _logger;

    public MongoWriterService(
        IMessageRepository messageRepository,
        ILogger<MongoWriterService> logger)
    {
        _messageChannel = Channel.CreateUnbounded<MessageDocument>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public ChannelWriter<MessageDocument> Writer => _messageChannel.Writer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MongoDB Writer Service starting...");

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogDebug("MongoWriterService: Received message {MessageId} from channel", message.Id);
            try
            {
                await _messageRepository.InsertAsync(message, stoppingToken);
                _logger.LogInformation("MongoWriterService: Message {MessageId} persisted to MongoDB (conversation={ConversationId}, sender={SenderId})",
                    message.Id, message.ConversationId, message.SenderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoWriterService: Error persisting message {MessageId} - {ErrorMessage}", message.Id, ex.Message);
            }
        }

        _logger.LogInformation("MongoDB Writer Service stopping...");
    }
}
