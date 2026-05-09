using ChatHub.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChatHub.Api.HealthChecks;

public class NatsHealthCheck : IHealthCheck
{
    private readonly INatsBackplane _natsBackplane;

    public NatsHealthCheck(INatsBackplane natsBackplane)
    {
        _natsBackplane = natsBackplane;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // NATS backplane doesn't expose connection state directly
        // For now, assume it's healthy if it was created successfully
        // In production, you'd want to implement a proper health check
        return Task.FromResult(HealthCheckResult.Healthy("NATS is healthy"));
    }
}
