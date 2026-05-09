using MongoDB.Driver;
using ChatHub.Core.Documents;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Options;

namespace ChatHub.Infrastructure.Persistence;

/// <summary>
/// Initializes MongoDB collections and indexes on startup
/// </summary>
public class MongoInitializer : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoInitializer> _logger;
    
    public MongoInitializer(
        IMongoClient client,
        IOptions<MongoSettings> settings,
        ILogger<MongoInitializer> logger)
    {
        _database = client.GetDatabase(settings.Value.DatabaseName);
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Initializing MongoDB indexes...");
        
        // Messages collection indexes
        var messagesCollection = _database.GetCollection<MessageDocument>("messages");
        
        var messageIndexKeys = Builders<MessageDocument>.IndexKeys
            .Ascending(m => m.ConversationId)
            .Descending(m => m.CreatedAt);
        await messagesCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<MessageDocument>(messageIndexKeys),
            cancellationToken: ct);
        
        var messageServiceIndex = Builders<MessageDocument>.IndexKeys
            .Ascending(m => m.ServiceId)
            .Descending(m => m.CreatedAt);
        await messagesCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<MessageDocument>(messageServiceIndex),
            cancellationToken: ct);
        
        // Conversations collection indexes
        var conversationsCollection = _database.GetCollection<ConversationDocument>("conversations");
        
        var conversationServiceIndex = Builders<ConversationDocument>.IndexKeys
            .Ascending(c => c.ServiceId);
        await conversationsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ConversationDocument>(conversationServiceIndex),
            cancellationToken: ct);
        
        var conversationParticipantIndex = Builders<ConversationDocument>.IndexKeys
            .Ascending(c => c.ParticipantIds);
        await conversationsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ConversationDocument>(conversationParticipantIndex),
            cancellationToken: ct);
        
        // Connections collection indexes with TTL
        var connectionsCollection = _database.GetCollection<ConnectionDocument>("connections");
        
        var connectionTTLIndex = Builders<ConnectionDocument>.IndexKeys
            .Ascending(c => c.DisconnectedAt);
        var connectionTTLOptions = new CreateIndexOptions
        {
            ExpireAfter = TimeSpan.FromHours(24)
        };
        await connectionsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ConnectionDocument>(connectionTTLIndex, connectionTTLOptions),
            cancellationToken: ct);
        
        _logger.LogInformation("MongoDB indexes created successfully");
    }
    
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
