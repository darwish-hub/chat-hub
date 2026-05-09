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
    private readonly INatsBackplane _natsBackplane;
    private readonly ILogger<MongoWriterService> _logger;

    public MongoWriterService(
        IMessageRepository messageRepository,
        INatsBackplane natsBackplane,
        ILogger<MongoWriterService> logger)
    {
        _messageChannel = Channel.CreateUnbounded<MessageDocument>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _messageRepository = messageRepository;
        _natsBackplane = natsBackplane;
        _logger = logger;
    }

    public ChannelWriter<MessageDocument> Writer => _messageChannel.Writer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MongoDB Writer Service starting...");

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Persist to MongoDB first (source of truth)
                await _messageRepository.InsertAsync(message, stoppingToken);
                _logger.LogDebug("Message {MessageId} persisted to MongoDB", message.Id);

                // Then publish to NATS for cross-pod fan-out
                var subject = $"chathub.{message.ServiceId}.messages";
                var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);
                await _natsBackplane.PublishAsync(subject, payload, stoppingToken);
                _logger.LogDebug("Message {MessageId} published to NATS subject {Subject}", message.Id, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            }
        }

        _logger.LogInformation("MongoDB Writer Service stopping...");
    }
}
