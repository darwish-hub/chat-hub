using ChatHub.Core.Documents;
using ChatHub.Infrastructure.Persistence;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace ChatHub.Tests.Integration;

public class MongoTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer;
    private MongoClient? _client;
    private IMongoDatabase? _database;

    public MongoTests()
    {
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        _client = new MongoClient(_mongoContainer.GetConnectionString());
        _database = _client.GetDatabase("chathub_test");
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    [Fact]
    public async Task CanInsertAndQueryMessage()
    {
        // Arrange
        var collection = _database!.GetCollection<MessageDocument>("messages");
        var message = new MessageDocument
        {
            Id = "msg-1",
            ConversationId = "conv-1",
            ServiceId = "svc-1",
            SenderId = "user-1",
            Type = "text",
            Text = "Hello, World!",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await collection.InsertOneAsync(message);
        var result = await collection.Find(m => m.Id == "msg-1").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello, World!", result.Text);
        Assert.Equal("conv-1", result.ConversationId);
    }

    [Fact]
    public async Task CanQueryByConversation()
    {
        // Arrange
        var collection = _database!.GetCollection<MessageDocument>("messages");
        var messages = new[]
        {
            new MessageDocument { Id = "msg-1", ConversationId = "conv-1", SenderId = "user-1", Type = "text", Text = "First", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new MessageDocument { Id = "msg-2", ConversationId = "conv-1", SenderId = "user-2", Type = "text", Text = "Second", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new MessageDocument { Id = "msg-3", ConversationId = "conv-2", SenderId = "user-1", Type = "text", Text = "Other", CreatedAt = DateTime.UtcNow }
        };
        await collection.InsertManyAsync(messages);

        // Act
        var results = await collection.Find(m => m.ConversationId == "conv-1").ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, m => Assert.Equal("conv-1", m.ConversationId));
    }

    [Fact]
    public async Task CanUpdateDeliveredAt()
    {
        // Arrange
        var collection = _database!.GetCollection<MessageDocument>("messages");
        var message = new MessageDocument
        {
            Id = "msg-1",
            ConversationId = "conv-1",
            SenderId = "user-1",
            Type = "text",
            Text = "Test",
            CreatedAt = DateTime.UtcNow
        };
        await collection.InsertOneAsync(message);

        // Act
        var deliveredAt = DateTime.UtcNow;
        var filter = Builders<MessageDocument>.Filter.Eq(m => m.Id, "msg-1");
        var update = Builders<MessageDocument>.Update.Set(m => m.DeliveredAt, deliveredAt);
        await collection.UpdateOneAsync(filter, update);

        // Assert
        var result = await collection.Find(m => m.Id == "msg-1").FirstOrDefaultAsync();
        Assert.NotNull(result);
        Assert.NotNull(result.DeliveredAt);
        Assert.True(result.DeliveredAt.Value > result.CreatedAt);
    }

    [Fact]
    public async Task CanQueryReplies()
    {
        // Arrange
        var collection = _database!.GetCollection<MessageDocument>("messages");
        var messages = new[]
        {
            new MessageDocument { Id = "msg-1", ConversationId = "conv-1", SenderId = "user-1", Type = "text", Text = "Original", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new MessageDocument { Id = "msg-2", ConversationId = "conv-1", SenderId = "user-2", Type = "text", Text = "Reply 1", ReplyToId = "msg-1", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new MessageDocument { Id = "msg-3", ConversationId = "conv-1", SenderId = "user-1", Type = "text", Text = "Reply 2", ReplyToId = "msg-1", CreatedAt = DateTime.UtcNow }
        };
        await collection.InsertManyAsync(messages);

        // Act
        var replies = await collection.Find(m => m.ReplyToId == "msg-1").ToListAsync();

        // Assert
        Assert.Equal(2, replies.Count);
        Assert.All(replies, m => Assert.Equal("msg-1", m.ReplyToId));
    }
}
