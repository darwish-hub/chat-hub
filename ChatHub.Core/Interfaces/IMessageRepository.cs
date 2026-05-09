using ChatHub.Core.Documents;

namespace ChatHub.Core.Interfaces;

public interface IMessageRepository
{
    Task<MessageDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<MessageDocument>> GetByConversationAsync(string conversationId, int limit = 50, CancellationToken ct = default);
    Task<IEnumerable<MessageDocument>> GetByConversationBeforeAsync(string conversationId, DateTime before, int limit = 50, CancellationToken ct = default);
    Task<IEnumerable<MessageDocument>> GetRepliesAsync(string messageId, CancellationToken ct = default);
    Task InsertAsync(MessageDocument message, CancellationToken ct = default);
    Task UpdateDeliveredAtAsync(string messageId, DateTime deliveredAt, CancellationToken ct = default);
}
