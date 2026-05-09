using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using StackExchange.Redis;

namespace ChatHub.Infrastructure.Cache;

public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _redis;
    private readonly int _textLimitPerMinute;
    private readonly int _voiceLimitPerMinute;

    public RedisRateLimiter(RedisSettings settings, ChatHub.Core.Settings.ChatHubSettings chatHubSettings)
    {
        var connection = ConnectionMultiplexer.Connect(settings.ConnectionString);
        _redis = connection.GetDatabase();
        _textLimitPerMinute = chatHubSettings.RateLimitTextPerMinute;
        _voiceLimitPerMinute = chatHubSettings.RateLimitVoicePerMinute;
    }

    public async Task<bool> CanSendTextAsync(string connectionId, CancellationToken ct = default)
    {
        return await CanPerformActionAsync($"ratelimit:text:{connectionId}", _textLimitPerMinute);
    }

    public async Task<bool> CanSendVoiceAsync(string connectionId, CancellationToken ct = default)
    {
        return await CanPerformActionAsync($"ratelimit:voice:{connectionId}", _voiceLimitPerMinute);
    }

    public async Task RecordTextAsync(string connectionId, CancellationToken ct = default)
    {
        await RecordActionAsync($"ratelimit:text:{connectionId}");
    }

    public async Task RecordVoiceAsync(string connectionId, CancellationToken ct = default)
    {
        await RecordActionAsync($"ratelimit:voice:{connectionId}");
    }

    private async Task<bool> CanPerformActionAsync(string key, int limit)
    {
        var current = await _redis.StringGetAsync(key);
        if (!current.HasValue)
            return true;

        return int.TryParse(current, out var count) && count < limit;
    }

    private async Task RecordActionAsync(string key)
    {
        var txn = _redis.CreateTransaction();
        txn.StringIncrementAsync(key);
        txn.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        await txn.ExecuteAsync();
    }
}
