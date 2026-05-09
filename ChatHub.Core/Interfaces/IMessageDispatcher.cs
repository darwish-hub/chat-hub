using ChatHub.Core.Models;

namespace ChatHub.Core.Interfaces;

public interface IMessageDispatcher
{
    Task DispatchAsync(string connectionId, ClientMessage message, CancellationToken ct);
}

public interface IMessageHandler<T> where T : ClientMessage
{
    Task HandleAsync(string connectionId, T message, CancellationToken ct);
}
