using ChatHub.Core.Documents;

namespace ChatHub.Core.Interfaces;

public interface IConversationRepository
{
    Task<ConversationDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IEnumerable<ConversationDocument>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<ConversationDocument>> GetByServiceAsync(string serviceId, CancellationToken ct = default);
    Task<IEnumerable<ConversationDocument>> GetAllAsync(CancellationToken ct = default);
    Task InsertAsync(ConversationDocument conversation, CancellationToken ct = default);
    Task UpdateLastMessageAtAsync(string conversationId, DateTime lastMessageAt, CancellationToken ct = default);
    Task<bool> IsParticipantAsync(string conversationId, string userId, CancellationToken ct = default);
    Task AddParticipantsAsync(string conversationId, IEnumerable<string> userIds, CancellationToken ct = default);
    Task<bool> JoinConversationAsync(string conversationId, string userId, CancellationToken ct = default);
}
