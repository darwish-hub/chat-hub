using StackExchange.Redis;
using ChatHub.Core.Interfaces;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Redis-backed voice session buffer for accumulating chunks
/// </summary>
public class RedisVoiceSessionBuffer : IVoiceSessionBuffer
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisVoiceSessionBuffer> _logger;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);
    
    public RedisVoiceSessionBuffer(
        IConnectionMultiplexer redis,
        ILogger<RedisVoiceSessionBuffer> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    public async Task AddChunkAsync(string messageId, int sequenceNumber, byte[] data, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        
        // Store chunk with sequence number as score
        await _database.SortedSetAddAsync(key, data, sequenceNumber);
        
        // Update TTL
        await _database.KeyExpireAsync(key, SessionTtl);
        
        _logger.LogDebug("Added voice chunk {SequenceNumber} for message {MessageId}", sequenceNumber, messageId);
    }
    
    public async Task<IReadOnlyList<byte[]>> GetChunksAsync(string messageId, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        
        // Get all chunks sorted by sequence number
        var entries = await _database.SortedSetRangeByRankAsync(key, 0, -1);
        
        return entries.Select(e => (byte[])e!).ToList();
    }
    
    public async Task DeleteSessionAsync(string messageId, CancellationToken ct = default)
    {
        var key = $"voice:{messageId}";
        await _database.KeyDeleteAsync(key);
        
        _logger.LogDebug("Deleted voice session for message {MessageId}", messageId);
    }
}
