using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using StackExchange.Redis;
using System.Text.Json;

namespace ChatHub.Infrastructure.Cache;

public class RedisPresenceService : IPresenceService
{
    private readonly IDatabase _redis;
    private readonly TimeSpan _presenceExpiry = TimeSpan.FromMinutes(2);

    public RedisPresenceService(RedisSettings settings)
    {
        var connection = ConnectionMultiplexer.Connect(settings.ConnectionString);
        _redis = connection.GetDatabase();
    }

    public async Task SetUserOnlineAsync(string serviceId, string userId, string connectionId, CancellationToken ct = default)
    {
        var key = $"presence:{serviceId}";
        var value = JsonSerializer.Serialize(new { UserId = userId, ConnectionId = connectionId, Timestamp = DateTime.UtcNow });
        
        await _redis.HashSetAsync(key, userId, value);
        await _redis.KeyExpireAsync(key, _presenceExpiry);
    }

    public async Task SetUserOfflineAsync(string serviceId, string userId, CancellationToken ct = default)
    {
        var key = $"presence:{serviceId}";
        await _redis.HashDeleteAsync(key, userId);
    }

    public async Task<IEnumerable<PresenceInfo>> GetOnlineUsersAsync(string serviceId, CancellationToken ct = default)
    {
        var key = $"presence:{serviceId}";
        var entries = await _redis.HashGetAllAsync(key);
        
        return entries.Select(e => JsonSerializer.Deserialize<PresenceInfo>(e.Value!)!);
    }

    public async Task<bool> IsUserOnlineAsync(string serviceId, string userId, CancellationToken ct = default)
    {
        var key = $"presence:{serviceId}";
        return await _redis.HashExistsAsync(key, userId);
    }

    public async Task StoreVoiceChunkAsync(string messageId, int sequenceNumber, byte[] chunk, bool isFinal, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        var value = Convert.ToBase64String(chunk);
        
        await _redis.SortedSetAddAsync(key, value, sequenceNumber);
        
        if (isFinal)
        {
            await _redis.KeyExpireAsync(key, TimeSpan.FromHours(1));
        }
    }

    public async Task<(byte[] chunk, int sequenceNumber)[]> GetVoiceChunksAsync(string messageId, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        var entries = await _redis.SortedSetRangeByRankWithScoresAsync(key);
        
        return entries.Select(e => (
            Convert.FromBase64String(e.Element!),
            (int)e.Score
        )).ToArray();
    }

    public async Task DeleteVoiceChunksAsync(string messageId, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        await _redis.KeyDeleteAsync(key);
    }
}

