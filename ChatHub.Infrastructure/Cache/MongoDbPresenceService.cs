using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ChatHub.Infrastructure.Cache;

public class MongoDbPresenceService : IPresenceService
{
    private readonly IMongoCollection<PresenceDocument> _collection;
    private readonly TimeSpan _presenceExpiry = TimeSpan.FromMinutes(2);
    private readonly string _podId;

    public MongoDbPresenceService(MongoInitializer mongoInitializer, ChatHubSettings chatHubSettings)
    {
        _collection = mongoInitializer.Database.GetCollection<PresenceDocument>("presence");
        _podId = chatHubSettings.PodId;
    }

    public async Task SetUserOnlineAsync(string serviceId, string userId, string connectionId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(_presenceExpiry);

        var filter = Builders<PresenceDocument>.Filter.Eq(p => p.ServiceId, serviceId)
            & Builders<PresenceDocument>.Filter.Eq(p => p.UserId, userId);

        var update = Builders<PresenceDocument>.Update
            .SetOnInsert(p => p.ServiceId, serviceId)
            .SetOnInsert(p => p.UserId, userId)
            .Set(p => p.ConnectionId, connectionId)
            .Set(p => p.PodId, _podId)
            .Set(p => p.UpdatedAt, now)
            .Set(p => p.ExpiresAt, expiresAt);

        var options = new UpdateOptions { IsUpsert = true };
        await _collection.UpdateOneAsync(filter, update, options, ct);
    }

    public async Task SetUserOfflineAsync(string serviceId, string userId, CancellationToken ct = default)
    {
        var filter = Builders<PresenceDocument>.Filter.Eq(p => p.ServiceId, serviceId)
            & Builders<PresenceDocument>.Filter.Eq(p => p.UserId, userId);
        await _collection.DeleteOneAsync(filter, ct);
    }

    public async Task<IEnumerable<PresenceInfo>> GetOnlineUsersAsync(string serviceId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<PresenceDocument>.Filter.Eq(p => p.ServiceId, serviceId)
            & Builders<PresenceDocument>.Filter.Gt(p => p.ExpiresAt, now);

        var docs = await _collection.Find(filter).ToListAsync(ct);
        return docs.Select(d => new PresenceInfo
        {
            UserId = d.UserId,
            ConnectionId = d.ConnectionId,
            Timestamp = d.UpdatedAt
        });
    }

    public async Task<bool> IsUserOnlineAsync(string serviceId, string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<PresenceDocument>.Filter.Eq(p => p.ServiceId, serviceId)
            & Builders<PresenceDocument>.Filter.Eq(p => p.UserId, userId)
            & Builders<PresenceDocument>.Filter.Gt(p => p.ExpiresAt, now);

        return await _collection.Find(filter).AnyAsync(ct);
    }
}
