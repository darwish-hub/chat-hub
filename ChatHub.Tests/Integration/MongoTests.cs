using Xunit;

namespace ChatHub.Tests.Integration;

public class MongoTests
{
    [Fact(Skip = "Requires MongoDB container - run manually")]
    public async Task CanInsertAndQuery()
    {
        // Integration test with Testcontainers.MongoDb
    }
}
