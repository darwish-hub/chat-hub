using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Manages voice message chunk storage in pod-local memory with sequence ordering and TTL cleanup.
/// </summary>
public class VoiceSessionBuffer
{
    private readonly ILogger<VoiceSessionBuffer> _logger;
    private readonly TimeSpan _sessionExpiry = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, VoiceSession> _sessions = new();

    public VoiceSessionBuffer(ILogger<VoiceSessionBuffer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Store a voice chunk in memory with sequence number
    /// </summary>
    public Task StoreChunkAsync(
        string messageId,
        int sequenceNumber,
        byte[] chunk,
        bool isFinal,
        CancellationToken ct = default)
    {
        var session = _sessions.GetOrAdd(messageId, _ => new VoiceSession());
        lock (session)
        {
            session.Chunks[sequenceNumber] = chunk;
            session.LastActivity = DateTime.UtcNow;
            if (isFinal)
            {
                session.IsComplete = true;
                session.TotalChunks = sequenceNumber + 1;
            }
        }

        _logger.LogDebug("Stored voice chunk {SequenceNumber} for message {MessageId}, size: {Size} bytes",
            sequenceNumber, messageId, chunk.Length);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieve all chunks for a voice message in sequence order
    /// </summary>
    public Task<VoiceChunk[]> GetChunksAsync(string messageId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(messageId, out var session))
        {
            lock (session)
            {
                var chunks = session.Chunks
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new VoiceChunk
                    {
                        Data = kv.Value,
                        SequenceNumber = kv.Key
                    })
                    .ToArray();
                return Task.FromResult(chunks);
            }
        }
        return Task.FromResult(Array.Empty<VoiceChunk>());
    }

    /// <summary>
    /// Get the highest sequence number stored for a message
    /// </summary>
    public Task<int> GetMaxSequenceNumberAsync(string messageId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(messageId, out var session))
        {
            lock (session)
            {
                return session.Chunks.Count > 0
                    ? Task.FromResult(session.Chunks.Keys.Max())
                    : Task.FromResult(-1);
            }
        }
        return Task.FromResult(-1);
    }

    /// <summary>
    /// Check if a voice session is marked as complete
    /// </summary>
    public Task<bool> IsCompleteAsync(string messageId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(messageId, out var session))
        {
            lock (session)
            {
                return Task.FromResult(session.IsComplete);
            }
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Get total expected chunks for a completed session
    /// </summary>
    public Task<int> GetTotalChunksAsync(string messageId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(messageId, out var session))
        {
            lock (session)
            {
                return Task.FromResult(session.TotalChunks);
            }
        }
        return Task.FromResult(0);
    }

    /// <summary>
    /// Delete all chunks and metadata for a voice message
    /// </summary>
    public Task DeleteSessionAsync(string messageId, CancellationToken ct = default)
    {
        _sessions.TryRemove(messageId, out _);
        _logger.LogDebug("Deleted voice session {MessageId}", messageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Assemble all chunks into a single byte array
    /// </summary>
    public Task<byte[]> AssembleAudioAsync(string messageId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(messageId, out var session))
            return Task.FromResult(Array.Empty<byte>());

        VoiceChunk[] chunks;
        lock (session)
        {
            chunks = session.Chunks
                .OrderBy(kv => kv.Key)
                .Select(kv => new VoiceChunk { SequenceNumber = kv.Key, Data = kv.Value })
                .ToArray();
        }

        if (chunks.Length == 0)
            return Task.FromResult(Array.Empty<byte>());

        var totalSize = chunks.Sum(c => c.Data.Length);
        var result = new byte[totalSize];

        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk.Data, 0, result, offset, chunk.Data.Length);
            offset += chunk.Data.Length;
        }

        _logger.LogInformation("Assembled voice message {MessageId} from {ChunkCount} chunks, total size: {Size} bytes",
            messageId, chunks.Length, totalSize);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Remove voice sessions that have not been active for longer than the expiry period.
    /// Returns the number of sessions removed.
    /// </summary>
    public int CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - _sessionExpiry;
        var expiredKeys = _sessions
            .Where(kv => kv.Value.LastActivity < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _sessions.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired voice sessions", expiredKeys.Count);
        }

        return expiredKeys.Count;
    }

    private class VoiceSession
    {
        public Dictionary<int, byte[]> Chunks { get; } = new();
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public bool IsComplete { get; set; }
        public int TotalChunks { get; set; }
    }
}

public class VoiceChunk
{
    public byte[] Data { get; set; } = null!;
    public int SequenceNumber { get; set; }
}
