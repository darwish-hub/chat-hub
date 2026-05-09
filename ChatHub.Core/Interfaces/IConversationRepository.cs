using ChatHub.Core.Documents;

namespace ChatHub.Core.Interfaces;

/// <summary>
/// Repository for conversation documents
/// </summary>
public interface IConversationRepository
{
    Task<ConversationDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDocument>> GetByServiceAsync(string serviceId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDocument>> GetByParticipantAsync(string userId, CancellationToken ct = default);
    Task InsertAsync(ConversationDocument conversation, CancellationToken ct = default);
    Task UpdateLastMessageAsync(string conversationId, CancellationToken ct = default);
}
