using ChatHub.Core.Models;

namespace ChatHub.Core.Interfaces;

/// <summary>
/// Dispatches client messages to appropriate handlers
/// </summary>
public interface IMessageDispatcher
{
    Task DispatchAsync(string connectionId, ClientMessage message, CancellationToken ct);
}

/// <summary>
/// Handles a specific type of client message
/// </summary>
public interface IMessageHandler<in T> where T : ClientMessage
{
    Task HandleAsync(string connectionId, T message, CancellationToken ct);
}
