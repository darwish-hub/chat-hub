using MongoDB.Driver;
using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Persistence;

/// <summary>
/// MongoDB repository for messages
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly IMongoCollection<MessageDocument> _collection;
    
    public MessageRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MessageDocument>("messages");
    }
    
    public async Task<MessageDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(m => m.Id == id).FirstOrDefaultAsync(ct);
    }
    
    public async Task<IReadOnlyList<MessageDocument>> GetByConversationAsync(
        string conversationId, 
        int limit, 
        DateTime? before = null, 
        CancellationToken ct = default)
    {
        var filter = Builders<MessageDocument>.Filter.Eq(m => m.ConversationId, conversationId);
        
        if (before.HasValue)
        {
            filter &= Builders<MessageDocument>.Filter.Lt(m => m.CreatedAt, before.Value);
        }
        
        return await _collection
            .Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);
    }
    
    public async Task<IReadOnlyList<MessageDocument>> GetByServiceAsync(
        string serviceId, 
        int limit, 
        DateTime? before = null, 
        CancellationToken ct = default)
    {
        var filter = Builders<MessageDocument>.Filter.Eq(m => m.ServiceId, serviceId);
        
        if (before.HasValue)
        {
            filter &= Builders<MessageDocument>.Filter.Lt(m => m.CreatedAt, before.Value);
        }
        
        return await _collection
            .Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);
    }
    
    public async Task InsertAsync(MessageDocument message, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(message, cancellationToken: ct);
    }
    
    public async Task MarkDeliveredAsync(string messageId, CancellationToken ct = default)
    {
        var filter = Builders<MessageDocument>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<MessageDocument>.Update.Set(m => m.DeliveredAt, DateTime.UtcNow);
        
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
