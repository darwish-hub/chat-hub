using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ChatHub.Infrastructure.Cache;

public class MongoDbRateLimiter : IRateLimiter
{
    private readonly IMongoCollection<RateLimitDocument> _collection;
    private readonly int _textLimitPerMinute;
    private readonly int _voiceLimitPerMinute;

    public MongoDbRateLimiter(MongoInitializer mongoInitializer, ChatHubSettings chatHubSettings)
    {
        _collection = mongoInitializer.Database.GetCollection<RateLimitDocument>("rate_limits");
        _textLimitPerMinute = chatHubSettings.RateLimitTextPerMinute;
        _voiceLimitPerMinute = chatHubSettings.RateLimitVoicePerMinute;
    }

    public async Task<bool> CanSendTextAsync(string connectionId, CancellationToken ct = default)
    {
        return await CanPerformActionAsync(connectionId, "text", _textLimitPerMinute, ct);
    }

    public async Task<bool> CanSendVoiceAsync(string connectionId, CancellationToken ct = default)
    {
        return await CanPerformActionAsync(connectionId, "voice", _voiceLimitPerMinute, ct);
    }

    public async Task RecordTextAsync(string connectionId, CancellationToken ct = default)
    {
        await RecordActionAsync(connectionId, "text", ct);
    }

    public async Task RecordVoiceAsync(string connectionId, CancellationToken ct = default)
    {
        await RecordActionAsync(connectionId, "voice", ct);
    }

    private async Task<bool> CanPerformActionAsync(string connectionId, string type, int limit, CancellationToken ct)
    {
        var windowStart = GetWindowStart();
        var filter = Builders<RateLimitDocument>.Filter.Eq(r => r.ConnectionId, connectionId)
            & Builders<RateLimitDocument>.Filter.Eq(r => r.Type, type)
            & Builders<RateLimitDocument>.Filter.Eq(r => r.WindowStart, windowStart);

        var doc = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return doc == null || doc.Count < limit;
    }

    private async Task RecordActionAsync(string connectionId, string type, CancellationToken ct)
    {
        var windowStart = GetWindowStart();
        var expiresAt = windowStart.AddMinutes(2); // TTL cleanup buffer

        var filter = Builders<RateLimitDocument>.Filter.Eq(r => r.ConnectionId, connectionId)
            & Builders<RateLimitDocument>.Filter.Eq(r => r.Type, type)
            & Builders<RateLimitDocument>.Filter.Eq(r => r.WindowStart, windowStart);

        var update = Builders<RateLimitDocument>.Update
            .SetOnInsert(r => r.ConnectionId, connectionId)
            .SetOnInsert(r => r.Type, type)
            .SetOnInsert(r => r.WindowStart, windowStart)
            .SetOnInsert(r => r.ExpiresAt, expiresAt)
            .Inc(r => r.Count, 1);

        var options = new FindOneAndUpdateOptions<RateLimitDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        await _collection.FindOneAndUpdateAsync(filter, update, options, ct);
    }

    private static DateTime GetWindowStart()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
    }
}
