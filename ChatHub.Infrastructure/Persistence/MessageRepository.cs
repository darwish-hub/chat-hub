using ChatHub.Core.Documents;
using ChatHub.Core.Interfaces;
using MongoDB.Driver;

namespace ChatHub.Infrastructure.Persistence;

public class MessageRepository : IMessageRepository
{
    private readonly IMongoCollection<MessageDocument> _collection;

    public MessageRepository(MongoInitializer mongoInitializer)
    {
        _collection = mongoInitializer.Database.GetCollection<MessageDocument>("messages");
    }

    public async Task<MessageDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(m => m.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<MessageDocument>> GetByConversationAsync(string conversationId, int limit = 50, CancellationToken ct = default)
    {
        return await _collection
            .Find(m => m.ConversationId == conversationId)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<MessageDocument>> GetByConversationBeforeAsync(string conversationId, DateTime before, int limit = 50, CancellationToken ct = default)
    {
        return await _collection
            .Find(m => m.ConversationId == conversationId && m.CreatedAt < before)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<MessageDocument>> GetRepliesAsync(string messageId, CancellationToken ct = default)
    {
        return await _collection
            .Find(m => m.ReplyToId == messageId)
            .SortBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task InsertAsync(MessageDocument message, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(message, cancellationToken: ct);
    }

    public async Task UpdateDeliveredAtAsync(string messageId, DateTime deliveredAt, CancellationToken ct = default)
    {
        var filter = Builders<MessageDocument>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<MessageDocument>.Update.Set(m => m.DeliveredAt, deliveredAt);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
