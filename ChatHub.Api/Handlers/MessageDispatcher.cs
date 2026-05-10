using System.Text.Json;
using System.Text.Json.Serialization;
using ChatHub.Core.Models;
using ChatHub.Core.Interfaces;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Dispatches client messages to registered handlers based on message type
/// </summary>
public class MessageDispatcher : IMessageDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageDispatcher> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public MessageDispatcher(
        IServiceProvider serviceProvider,
        ILogger<MessageDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(string connectionId, ClientMessage message, CancellationToken ct)
    {
        _logger.LogDebug("Dispatching message type {MessageType} for connection {ConnectionId}",
            message.Type, connectionId);

        switch (message.Type)
        {
            case "join_service":
                await HandleAsync<JoinServiceMessage>(connectionId, message, ct);
                break;
            case "leave_service":
                await HandleAsync<LeaveServiceMessage>(connectionId, message, ct);
                break;
            case "text_message":
                await HandleAsync<TextMessage>(connectionId, message, ct);
                break;
            case "voice_chunk":
                await HandleAsync<VoiceChunkMessage>(connectionId, message, ct);
                break;
            case "voice_message":
                await HandleAsync<VoiceMessage>(connectionId, message, ct);
                break;
            case "file_attachment":
                await HandleAsync<FileAttachmentMessage>(connectionId, message, ct);
                break;
            case "typing":
                await HandleAsync<TypingMessage>(connectionId, message, ct);
                break;
            case "ack":
                await HandleAsync<AckMessage>(connectionId, message, ct);
                break;
            case "pong":
                HandlePong(connectionId);
                break;
            default:
                _logger.LogWarning("Unknown message type {MessageType}", message.Type);
                break;
        }
    }

    private async Task HandleAsync<T>(string connectionId, ClientMessage message, CancellationToken ct)
        where T : ClientMessage
    {
        // Serialize and deserialize to get the concrete type
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var concreteMessage = JsonSerializer.Deserialize<T>(json, JsonOptions);

        if (concreteMessage == null)
        {
            _logger.LogError("Failed to deserialize message to type {Type}", typeof(T).Name);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService<IMessageHandler<T>>();

        if (handler != null)
        {
            await handler.HandleAsync(connectionId, concreteMessage, ct);
        }
        else
        {
            _logger.LogWarning("No handler registered for message type {Type}", typeof(T).Name);
        }
    }

    private void HandlePong(string connectionId)
    {
        var registry = _serviceProvider.GetRequiredService<IConnectionRegistry>();
        var connection = registry.Get(connectionId);

        if (connection is Core.Interfaces.IWebSocketConnection wsConnection)
        {
            wsConnection.UpdateLastPong();
        }
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
