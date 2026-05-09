using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ChatHub.Api.HealthChecks;

/// <summary>
/// Health check for Redis connection
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;
    
    public RedisHealthCheck(
        IConnectionMultiplexer redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            if (_redis.IsConnected)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Redis connected"));
            }
            
            return Task.FromResult(HealthCheckResult.Unhealthy("Redis not connected"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Redis check failed", ex));
        }
    }
}
