using MongoDB.Driver;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;

namespace ChatHub.Infrastructure.Persistence;

/// <summary>
/// MongoDB repository for conversations
/// </summary>
public class ConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<ConversationDocument> _collection;
    
    public ConversationRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ConversationDocument>("conversations");
    }
    
    public async Task<ConversationDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(c => c.Id == id).FirstOrDefaultAsync(ct);
    }
    
    public async Task<IReadOnlyList<ConversationDocument>> GetByServiceAsync(string serviceId, CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.ServiceId == serviceId)
            .SortByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);
    }
    
    public async Task<IReadOnlyList<ConversationDocument>> GetByParticipantAsync(string userId, CancellationToken ct = default)
    {
        return await _collection
            .Find(c => c.ParticipantIds.Contains(userId))
            .SortByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);
    }
    
    public async Task InsertAsync(ConversationDocument conversation, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(conversation, cancellationToken: ct);
    }
    
    public async Task UpdateLastMessageAsync(string conversationId, CancellationToken ct = default)
    {
        var filter = Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId);
        var update = Builders<ConversationDocument>.Update.Set(c => c.LastMessageAt, DateTime.UtcNow);
        
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
