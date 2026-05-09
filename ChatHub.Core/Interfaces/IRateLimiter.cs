namespace ChatHub.Core.Interfaces;

public interface IRateLimiter
{
    Task<bool> CanSendTextAsync(string connectionId, CancellationToken ct = default);
    Task<bool> CanSendVoiceAsync(string connectionId, CancellationToken ct = default);
    Task RecordTextAsync(string connectionId, CancellationToken ct = default);
    Task RecordVoiceAsync(string connectionId, CancellationToken ct = default);
}
