using System.Text.Json;
using System.Threading.Channels;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;

namespace ChatHub.Infrastructure.Writers;

/// <summary>
/// Background service that drains the MongoDB write channel and publishes to NATS after successful write
/// </summary>
public class MongoWriterService : BackgroundService
{
    private readonly Channel<MessageWriteItem> _channel;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly INatsBackplane _natsBackplane;
    private readonly ILogger<MongoWriterService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public MongoWriterService(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        INatsBackplane natsBackplane,
        ILogger<MongoWriterService> logger)
    {
        _channel = Channel.CreateUnbounded<MessageWriteItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _natsBackplane = natsBackplane;
        _logger = logger;
    }
    
    public void QueueWrite(MessageDocument message, MessageEnvelope envelope)
    {
        _channel.Writer.TryWrite(new MessageWriteItem(message, envelope));
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // 1. Persist to MongoDB first
                await _messageRepository.InsertAsync(item.Message, stoppingToken);
                
                // 2. Update conversation last message time
                await _conversationRepository.UpdateLastMessageAsync(
                    item.Message.ConversationId, stoppingToken);
                
                // 3. Publish to NATS after successful write
                var payload = JsonSerializer.SerializeToUtf8Bytes(item.Envelope, JsonOptions);
                var subject = $"chathub.{item.Message.ServiceId}.messages";
                
                await _natsBackplane.PublishAsync(subject, payload, stoppingToken);
                
                _logger.LogDebug(
                    "Message {MessageId} persisted and published to NATS",
                    item.Message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to process message {MessageId}", 
                    item.Message.Id);
            }
        }
    }
    
    private record MessageWriteItem(MessageDocument Message, MessageEnvelope Envelope);
}
