using ChatHub.Core.Documents;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ChatHub.Infrastructure.Persistence;

public class MongoInitializer : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoInitializer> _logger;

    public MongoInitializer(MongoSettings settings, ILogger<MongoInitializer> logger)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        _logger = logger;
    }

    public IMongoDatabase Database => _database;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing MongoDB collections and indexes...");

        // Messages collection
        var messagesCollection = _database.GetCollection<MessageDocument>("messages");
        
        var messageIndexes = Builders<MessageDocument>.IndexKeys;
        await messagesCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<MessageDocument>(
                messageIndexes.Ascending(m => m.ConversationId).Descending(m => m.CreatedAt)),
            new CreateIndexModel<MessageDocument>(
                messageIndexes.Ascending(m => m.ServiceId).Descending(m => m.CreatedAt)),
            new CreateIndexModel<MessageDocument>(
                messageIndexes.Ascending(m => m.SenderId).Descending(m => m.CreatedAt))
        }, cancellationToken);

        // Conversations collection
        var conversationsCollection = _database.GetCollection<ConversationDocument>("conversations");
        
        var conversationIndexes = Builders<ConversationDocument>.IndexKeys;
        await conversationsCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ConversationDocument>(
                conversationIndexes.Ascending(c => c.ServiceId)),
            new CreateIndexModel<ConversationDocument>(
                conversationIndexes.Ascending(c => c.ParticipantIds)),
            new CreateIndexModel<ConversationDocument>(
                conversationIndexes.Ascending(c => c.ServiceId).Descending(c => c.LastMessageAt))
        }, cancellationToken);

        // Connections collection (ephemeral)
        var connectionsCollection = _database.GetCollection<ConnectionDocument>("connections");
        
        var connectionIndexes = Builders<ConnectionDocument>.IndexKeys;
        await connectionsCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ConnectionDocument>(
                connectionIndexes.Ascending(c => c.UserId).Descending(c => c.ConnectedAt)),
            new CreateIndexModel<ConnectionDocument>(
                connectionIndexes.Ascending(c => c.ConnectionId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<ConnectionDocument>(
                connectionIndexes.Ascending(c => c.DisconnectedAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }) // TTL index
        }, cancellationToken);

        _logger.LogInformation("MongoDB initialization complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
