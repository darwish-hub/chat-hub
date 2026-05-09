using ChatHub.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChatHub.Api.HealthChecks;

public class MongoHealthCheck : IHealthCheck
{
    private readonly MongoInitializer _mongoInitializer;

    public MongoHealthCheck(MongoInitializer mongoInitializer)
    {
        _mongoInitializer = mongoInitializer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to ping MongoDB
            await _mongoInitializer.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Driver.BsonDocumentCommand<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1)),
                cancellationToken: cancellationToken);
            
            return HealthCheckResult.Healthy("MongoDB is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unhealthy", ex);
        }
    }
}
