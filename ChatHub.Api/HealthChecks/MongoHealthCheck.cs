using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace ChatHub.Api.HealthChecks;

/// <summary>
/// Health check for MongoDB connection
/// </summary>
public class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;
    private readonly ILogger<MongoHealthCheck> _logger;
    
    public MongoHealthCheck(
        IMongoClient client,
        ILogger<MongoHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            // Ping MongoDB
            await _client.ListDatabaseNamesAsync(ct);
            return HealthCheckResult.Healthy("MongoDB connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed");
            return HealthCheckResult.Unhealthy("MongoDB check failed", ex);
        }
    }
}
