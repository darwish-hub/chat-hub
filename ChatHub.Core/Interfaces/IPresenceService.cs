namespace ChatHub.Core.Interfaces;

/// <summary>
/// Redis-backed presence service
/// </summary>
public interface IPresenceService
{
    Task SetUserOnlineAsync(string serviceId, string userId, string connectionId, CancellationToken ct = default);
    Task SetUserOfflineAsync(string serviceId, string userId, CancellationToken ct = default);
    Task<IEnumerable<PresenceInfo>> GetOnlineUsersAsync(string serviceId, CancellationToken ct = default);
    Task<bool> IsUserOnlineAsync(string serviceId, string userId, CancellationToken ct = default);
}

public class PresenceInfo
{
    public string UserId { get; set; } = null!;
    public string ConnectionId { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}
