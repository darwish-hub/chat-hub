namespace ChatHub.Core.Interfaces;

/// <summary>
/// Rate limiter backed by Redis sliding window
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Check if an operation is allowed within rate limits
    /// </summary>
    /// <param name="key">Unique key for the rate limit (e.g., "text:{connectionId}")</param>
    /// <param name="limit">Maximum allowed operations</param>
    /// <param name="window">Time window for the limit</param>
    /// <returns>True if allowed, false if rate limited</returns>
    Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default);
}
