namespace ChatHub.Core.Interfaces;

public interface INatsBackplane
{
    Task PublishAsync(string subject, ReadOnlyMemory<byte> payload, CancellationToken ct = default);
    Task SubscribeAsync(string subject, string? queueGroup, Func<string, ReadOnlyMemory<byte>, Task> handler, CancellationToken ct = default);
}
