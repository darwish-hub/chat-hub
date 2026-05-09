using ChatHub.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChatHub.Api.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IPresenceService _presenceService;

    public RedisHealthCheck(IPresenceService presenceService)
    {
        _presenceService = presenceService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try a simple operation
            await _presenceService.IsUserOnlineAsync("health-check", "test", cancellationToken);
            return HealthCheckResult.Healthy("Redis is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unhealthy", ex);
        }
    }
}
