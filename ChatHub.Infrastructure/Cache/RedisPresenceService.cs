using StackExchange.Redis;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Redis-backed presence service
/// </summary>
public class RedisPresenceService : IPresenceService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisPresenceService> _logger;
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromMinutes(5);
    
    public RedisPresenceService(
        IConnectionMultiplexer redis,
        ILogger<RedisPresenceService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    public async Task SetOnlineAsync(string userId, string connectionId, CancellationToken ct = default)
    {
        var userKey = $"presence:user:{userId}";
        var serviceKey = $"presence:connection:{connectionId}";
        
        await _database.StringSetAsync(userKey, connectionId, PresenceTtl);
        await _database.StringSetAsync(serviceKey, userId, PresenceTtl);
        
        _logger.LogDebug("User {UserId} is now online with connection {ConnectionId}", userId, connectionId);
    }
    
    public async Task SetOfflineAsync(string userId, string connectionId, CancellationToken ct = default)
    {
        var userKey = $"presence:user:{userId}";
        var serviceKey = $"presence:connection:{connectionId}";
        
        await _database.KeyDeleteAsync(userKey);
        await _database.KeyDeleteAsync(serviceKey);
        
        _logger.LogDebug("User {UserId} is now offline", userId);
    }
    
    public async Task<bool> IsOnlineAsync(string userId, CancellationToken ct = default)
    {
        var userKey = $"presence:user:{userId}";
        return await _database.KeyExistsAsync(userKey);
    }
    
    public Task<IReadOnlyList<string>> GetOnlineUsersAsync(string serviceId, CancellationToken ct = default)
    {
        // For service-scoped presence, we would need to track service membership
        // This is a simplified implementation
        return Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }
}
