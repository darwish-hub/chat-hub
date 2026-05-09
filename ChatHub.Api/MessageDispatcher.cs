using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;

namespace ChatHub.Api;

public class MessageDispatcher : IMessageDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IServiceProvider serviceProvider, ILogger<MessageDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(string connectionId, ClientMessage message, CancellationToken ct)
    {
        try
        {
            switch (message)
            {
                case JoinServiceMessage joinService:
                    await HandleAsync<IJoinServiceHandler, JoinServiceMessage>(connectionId, joinService, ct);
                    break;
                case LeaveServiceMessage leaveService:
                    await HandleAsync<ILeaveServiceHandler, LeaveServiceMessage>(connectionId, leaveService, ct);
                    break;
                case TextMessage textMessage:
                    await HandleAsync<ITextMessageHandler, TextMessage>(connectionId, textMessage, ct);
                    break;
                case VoiceChunkMessage voiceChunk:
                    await HandleAsync<IVoiceChunkHandler, VoiceChunkMessage>(connectionId, voiceChunk, ct);
                    break;
                case VoiceMessage voiceMessage:
                    await HandleAsync<IVoiceMessageHandler, VoiceMessage>(connectionId, voiceMessage, ct);
                    break;
                case FileAttachmentMessage fileAttachment:
                    await HandleAsync<IFileAttachmentHandler, FileAttachmentMessage>(connectionId, fileAttachment, ct);
                    break;
                case TypingMessage typing:
                    await HandleAsync<ITypingHandler, TypingMessage>(connectionId, typing, ct);
                    break;
                case AckMessage ack:
                    await HandleAsync<IAckHandler, AckMessage>(connectionId, ack, ct);
                    break;
                case PongMessage pong:
                    await HandleAsync<IPongHandler, PongMessage>(connectionId, pong, ct);
                    break;
                default:
                    _logger.LogWarning("Unknown message type received: {MessageType}", message.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching message of type {MessageType}", message.GetType().Name);
            throw;
        }
    }

    private async Task HandleAsync<THandler, TMessage>(string connectionId, TMessage message, CancellationToken ct)
        where THandler : IMessageHandler<TMessage>
        where TMessage : ClientMessage
    {
        var handler = _serviceProvider.GetRequiredService<THandler>();
        await handler.HandleAsync(connectionId, message, ct);
    }
}

// Handler interfaces
public interface IJoinServiceHandler : IMessageHandler<JoinServiceMessage> { }
public interface ILeaveServiceHandler : IMessageHandler<LeaveServiceMessage> { }
public interface ITextMessageHandler : IMessageHandler<TextMessage> { }
public interface IVoiceChunkHandler : IMessageHandler<VoiceChunkMessage> { }
public interface IVoiceMessageHandler : IMessageHandler<VoiceMessage> { }
public interface IFileAttachmentHandler : IMessageHandler<FileAttachmentMessage> { }
public interface ITypingHandler : IMessageHandler<TypingMessage> { }
public interface IAckHandler : IMessageHandler<AckMessage> { }
public interface IPongHandler : IMessageHandler<PongMessage> { }
