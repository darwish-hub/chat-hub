namespace ChatHub.Core.Interfaces;

/// <summary>
/// Voice session buffer for accumulating voice chunks in pod-local memory.
/// </summary>
public interface IVoiceSessionBuffer
{
    /// <summary>
    /// Add a voice chunk to the session
    /// </summary>
    Task AddChunkAsync(string messageId, int sequenceNumber, byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Get all chunks for a message in sequence order
    /// </summary>
    Task<IReadOnlyList<byte[]>> GetChunksAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Delete the session and all chunks
    /// </summary>
    Task DeleteSessionAsync(string messageId, CancellationToken ct = default);
}
