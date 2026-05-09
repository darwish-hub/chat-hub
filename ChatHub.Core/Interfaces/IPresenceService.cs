namespace ChatHub.Core.Interfaces;

/// <summary>
/// Redis-backed presence service
/// </summary>
public interface IPresenceService
{
    Task SetOnlineAsync(string userId, string connectionId, CancellationToken ct = default);
    Task SetOfflineAsync(string userId, string connectionId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetOnlineUsersAsync(string serviceId, CancellationToken ct = default);
}
