namespace ChatHub.Core.Interfaces;

/// <summary>
/// NATS backplane for cross-pod message fan-out
/// </summary>
public interface INatsBackplane
{
    /// <summary>
    /// Publish a message to a NATS subject
    /// </summary>
    Task PublishAsync(string subject, ReadOnlyMemory<byte> payload, CancellationToken ct = default);
    
    /// <summary>
    /// Subscribe to a subject pattern with an optional queue group
    /// </summary>
    Task SubscribeAsync(string subject, string? queueGroup, Func<string, ReadOnlyMemory<byte>, Task> handler, CancellationToken ct);
}
