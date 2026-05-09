using ChatHub.Core.Documents;

namespace ChatHub.Core.Interfaces;

/// <summary>
/// Repository for message documents
/// </summary>
public interface IMessageRepository
{
    Task<MessageDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDocument>> GetByConversationAsync(string conversationId, int limit, DateTime? before = null, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDocument>> GetByServiceAsync(string serviceId, int limit, DateTime? before = null, CancellationToken ct = default);
    Task InsertAsync(MessageDocument message, CancellationToken ct = default);
    Task MarkDeliveredAsync(string messageId, CancellationToken ct = default);
}
