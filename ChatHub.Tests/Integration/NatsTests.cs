using NATS.Client.Core;
using Xunit;

namespace ChatHub.Tests.Integration;

public class NatsTests : IAsyncLifetime
{
    private NatsConnection? _connection;

    public async Task InitializeAsync()
    {
        var options = new NatsOpts
        {
            Url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222",
            ConnectTimeout = TimeSpan.FromSeconds(5),
            RequestTimeout = TimeSpan.FromSeconds(5)
        };
        _connection = new NatsConnection(options);
        await _connection.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact(Skip = "Requires NATS server - run with NATS_URL env var")]
    public async Task CanPublishAndSubscribe()
    {
        // Arrange
        var subject = $"test.{Guid.NewGuid()}";
        var receivedMessages = new List<string>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var subscription = Task.Run(async () =>
        {
            await foreach (var msg in _connection!.SubscribeAsync<string>(subject, cancellationToken: cts.Token))
            {
                receivedMessages.Add(msg.Data!);
                if (receivedMessages.Count >= 2) break;
            }
        });

        await _connection!.PublishAsync(subject, "message-1");
        await _connection!.PublishAsync(subject, "message-2");

        await subscription;

        // Assert
        Assert.Equal(2, receivedMessages.Count);
        Assert.Contains("message-1", receivedMessages);
        Assert.Contains("message-2", receivedMessages);
    }

    [Fact(Skip = "Requires NATS server - run with NATS_URL env var")]
    public async Task CanUseRequestReply()
    {
        // Arrange
        var subject = $"test.req.{Guid.NewGuid()}";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Set up reply handler
        _ = Task.Run(async () =>
        {
            await foreach (var msg in _connection!.SubscribeAsync<string>(subject, cancellationToken: cts.Token))
            {
                await msg.ReplyAsync($"reply-to-{msg.Data}");
                break;
            }
        });

        // Act
        var response = await _connection!.RequestAsync<string, string>(subject, "test", cancellationToken: cts.Token);

        // Assert
        Assert.Equal("reply-to-test", response.Data);
    }
}
