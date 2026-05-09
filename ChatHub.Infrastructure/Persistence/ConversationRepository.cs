using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using MongoDB.Driver;

namespace ChatHub.Infrastructure.Persistence;

public class ConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<ConversationDocument> _collection;

    public ConversationRepository(MongoInitializer mongoInitializer)
    {
        _collection = mongoInitializer.Database.GetCollection<ConversationDocument>("conversations");
    }

    public async Task<ConversationDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(c => c.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<ConversationDocument>> GetByUserAsync(string userId, CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.ParticipantIds.Contains(userId))
            .SortByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ConversationDocument>> GetByServiceAsync(string serviceId, CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.ServiceId == serviceId)
            .SortByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);
    }

    public async Task InsertAsync(ConversationDocument conversation, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(conversation, cancellationToken: ct);
    }

    public async Task UpdateLastMessageAtAsync(string conversationId, DateTime lastMessageAt, CancellationToken ct = default)
    {
        var filter = Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<ConversationDocument>.Update.Set(c => c.LastMessageAt, lastMessageAt);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<bool> IsParticipantAsync(string conversationId, string userId, CancellationToken ct = default)
    {
        var count = await _collection.CountDocumentsAsync(
            c => c.Id == conversationId && c.ParticipantIds.Contains(userId),
            cancellationToken: ct);
        return count > 0;
    }
}
