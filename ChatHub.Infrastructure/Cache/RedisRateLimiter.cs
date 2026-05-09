using StackExchange.Redis;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Redis sliding window rate limiter
/// </summary>
public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisRateLimiter> _logger;
    
    public RedisRateLimiter(
        IConnectionMultiplexer redis,
        ILogger<RedisRateLimiter> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    public async Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var redisKey = $"ratelimit:{key}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (long)window.TotalSeconds;
        
        // Use Redis sorted set for sliding window
        // Remove old entries outside the window
        await _database.SortedSetRemoveRangeByScoreAsync(redisKey, 0, windowStart);
        
        // Get current count
        var currentCount = await _database.SortedSetLengthAsync(redisKey);
        
        if (currentCount >= limit)
        {
            _logger.LogWarning("Rate limit exceeded for key {Key}", key);
            return false;
        }
        
        // Add current request
        await _database.SortedSetAddAsync(redisKey, Guid.NewGuid().ToString(), now);
        
        // Set expiry on the key
        await _database.KeyExpireAsync(redisKey, window);
        
        return true;
    }
}
