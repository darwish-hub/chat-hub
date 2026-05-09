using Xunit;

namespace ChatHub.Tests.Integration;

public class NatsTests
{
    [Fact(Skip = "Requires NATS container - run manually")]
    public async Task CanPublishAndSubscribe()
    {
        // Integration test with Testcontainers.Nats
    }
}
