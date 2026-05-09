using Microsoft.Extensions.Diagnostics.HealthChecks;
using NATS.Client;

namespace ChatHub.Api.HealthChecks;

/// <summary>
/// Health check for NATS connection
/// </summary>
public class NatsHealthCheck : IHealthCheck
{
    private readonly IConnection _connection;
    private readonly ILogger<NatsHealthCheck> _logger;
    
    public NatsHealthCheck(
        IConnection connection,
        ILogger<NatsHealthCheck> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            if (_connection.State == ConnState.CONNECTED)
            {
                return Task.FromResult(HealthCheckResult.Healthy("NATS connected"));
            }
            
            return Task.FromResult(HealthCheckResult.Unhealthy($"NATS state: {_connection.State}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NATS health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("NATS check failed", ex));
        }
    }
}
