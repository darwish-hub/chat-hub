using ChatHub.Core.Settings;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Manages voice message chunk storage in Redis using sorted sets for sequence ordering
/// </summary>
public class VoiceSessionBuffer
{
    private readonly IDatabase _redis;
    private readonly ILogger<VoiceSessionBuffer> _logger;
    private readonly TimeSpan _sessionExpiry = TimeSpan.FromHours(1);

    public VoiceSessionBuffer(RedisSettings settings, ILogger<VoiceSessionBuffer> logger)
    {
        var connection = ConnectionMultiplexer.Connect(settings.ConnectionString);
        _redis = connection.GetDatabase();
        _logger = logger;
    }

    /// <summary>
    /// Store a voice chunk in Redis with sequence number
    /// </summary>
    public async Task StoreChunkAsync(
        string messageId, 
        int sequenceNumber, 
        byte[] chunk, 
        bool isFinal,
        CancellationToken ct = default)
    {
        var key = GetKey(messageId);
        var base64Chunk = Convert.ToBase64String(chunk);
        
        // Store in sorted set with sequence number as score
        await _redis.SortedSetAddAsync(key, base64Chunk, sequenceNumber);
        
        // Set expiry on the key
        await _redis.KeyExpireAsync(key, _sessionExpiry);
        
        // If final chunk, store metadata
        if (isFinal)
        {
            var metaKey = GetMetaKey(messageId);
            await _redis.HashSetAsync(metaKey, new HashEntry[]
            {
                new HashEntry("isComplete", true),
                new HashEntry("totalChunks", sequenceNumber + 1),
                new HashEntry("completedAt", DateTime.UtcNow.ToString("O"))
            });
            await _redis.KeyExpireAsync(metaKey, _sessionExpiry);
        }
        
        _logger.LogDebug("Stored voice chunk {SequenceNumber} for message {MessageId}, size: {Size} bytes",
            sequenceNumber, messageId, chunk.Length);
    }

    /// <summary>
    /// Retrieve all chunks for a voice message in sequence order
    /// </summary>
    public async Task<VoiceChunk[]> GetChunksAsync(string messageId, CancellationToken ct = default)
    {
        var key = GetKey(messageId);
        var entries = await _redis.SortedSetRangeByRankWithScoresAsync(key);
        
        return entries.Select(e => new VoiceChunk
        {
            Data = Convert.FromBase64String(e.Element!),
            SequenceNumber = (int)e.Score
        }).ToArray();
    }

    /// <summary>
    /// Get the highest sequence number stored for a message
    /// </summary>
    public async Task<int> GetMaxSequenceNumberAsync(string messageId, CancellationToken ct = default)
    {
        var key = GetKey(messageId);
        var max = await _redis.SortedSetRangeByRankAsync(key, -1, -1, Order.Ascending);
        
        if (max.Length == 0)
            return -1;
        
        var score = await _redis.SortedSetScoreAsync(key, max[0]);
        return score.HasValue ? (int)score.Value : -1;
    }

    /// <summary>
    /// Check if a voice session is marked as complete
    /// </summary>
    public async Task<bool> IsCompleteAsync(string messageId, CancellationToken ct = default)
    {
        var metaKey = GetMetaKey(messageId);
        var isComplete = await _redis.HashGetAsync(metaKey, "isComplete");
        return isComplete.HasValue && (bool)isComplete;
    }

    /// <summary>
    /// Get total expected chunks for a completed session
    /// </summary>
    public async Task<int> GetTotalChunksAsync(string messageId, CancellationToken ct = default)
    {
        var metaKey = GetMetaKey(messageId);
        var total = await _redis.HashGetAsync(metaKey, "totalChunks");
        return total.HasValue ? (int)total : 0;
    }

    /// <summary>
    /// Delete all chunks and metadata for a voice message
    /// </summary>
    public async Task DeleteSessionAsync(string messageId, CancellationToken ct = default)
    {
        var key = GetKey(messageId);
        var metaKey = GetMetaKey(messageId);
        
        await _redis.KeyDeleteAsync(new RedisKey[] { key, metaKey });
        
        _logger.LogDebug("Deleted voice session {MessageId}", messageId);
    }

    /// <summary>
    /// Assemble all chunks into a single byte array
    /// </summary>
    public async Task<byte[]> AssembleAudioAsync(string messageId, CancellationToken ct = default)
    {
        var chunks = await GetChunksAsync(messageId, ct);
        
        if (chunks.Length == 0)
            return Array.Empty<byte>();
        
        // Calculate total size
        var totalSize = chunks.Sum(c => c.Data.Length);
        var result = new byte[totalSize];
        
        // Copy chunks in order
        var offset = 0;
        foreach (var chunk in chunks.OrderBy(c => c.SequenceNumber))
        {
            Buffer.BlockCopy(chunk.Data, 0, result, offset, chunk.Data.Length);
            offset += chunk.Data.Length;
        }
        
        _logger.LogInformation("Assembled voice message {MessageId} from {ChunkCount} chunks, total size: {Size} bytes",
            messageId, chunks.Length, totalSize);
        
        return result;
    }

    private static string GetKey(string messageId) => $"voice:chunks:{messageId}";
    private static string GetMetaKey(string messageId) => $"voice:meta:{messageId}";
}

public class VoiceChunk
{
    public byte[] Data { get; set; } = null!;
    public int SequenceNumber { get; set; }
}
