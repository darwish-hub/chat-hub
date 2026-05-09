using ChatHub.Core.Interfaces;
using ChatHub.Infrastructure.WebSockets;
using FluentAssertions;
using Xunit;

namespace ChatHub.Tests.Unit.WebSockets;

public class ConnectionRegistryTests
{
    private readonly ConnectionRegistry _registry;

    public ConnectionRegistryTests()
    {
        _registry = new ConnectionRegistry();
    }

    [Fact]
    public void Register_ShouldAddConnection()
    {
        var connection = new TestWebSocketConnection("conn-1", "user-1");

        _registry.Register(connection);

        _registry.Connections.Should().ContainKey("conn-1");
        _registry.GetConnection("conn-1").Should().Be(connection);
    }

    [Fact]
    public void Deregister_ShouldRemoveConnection()
    {
        var connection = new TestWebSocketConnection("conn-1", "user-1");
        _registry.Register(connection);

        _registry.Deregister("conn-1");

        _registry.Connections.Should().NotContainKey("conn-1");
        _registry.GetConnection("conn-1").Should().BeNull();
    }

    [Fact]
    public void AddToService_ShouldIndexByService()
    {
        var connection = new TestWebSocketConnection("conn-1", "user-1");
        _registry.Register(connection);

        _registry.AddToService("conn-1", "service-a");

        _registry.ServiceIndex.Should().ContainKey("service-a");
        _registry.GetConnectionsByService("service-a").Should().Contain(connection);
    }

    [Fact]
    public void RemoveFromService_ShouldUpdateIndex()
    {
        var connection = new TestWebSocketConnection("conn-1", "user-1");
        _registry.Register(connection);
        _registry.AddToService("conn-1", "service-a");

        _registry.RemoveFromService("conn-1", "service-a");

        _registry.GetConnectionsByService("service-a").Should().BeEmpty();
    }

    [Fact]
    public void GetConnectionsByUser_ShouldReturnUserConnections()
    {
        var conn1 = new TestWebSocketConnection("conn-1", "user-1");
        var conn2 = new TestWebSocketConnection("conn-2", "user-1");
        _registry.Register(conn1);
        _registry.Register(conn2);

        var connections = _registry.GetConnectionsByUser("user-1");

        connections.Should().HaveCount(2);
        connections.Should().Contain(conn1);
        connections.Should().Contain(conn2);
    }

    private class TestWebSocketConnection : IWebSocketConnection
    {
        public string ConnectionId { get; }
        public string UserId { get; }
        public System.Security.Claims.ClaimsPrincipal User { get; }
        public DateTime ConnectedAt { get; }
        public DateTime LastPongAt { get; private set; }
        public HashSet<string> JoinedServices { get; } = new();

        public TestWebSocketConnection(string connectionId, string userId)
        {
            ConnectionId = connectionId;
            UserId = userId;
            User = new System.Security.Claims.ClaimsPrincipal();
            ConnectedAt = DateTime.UtcNow;
            LastPongAt = DateTime.UtcNow;
        }

        public void UpdateLastPong() => LastPongAt = DateTime.UtcNow;
        public void JoinService(string serviceId) => JoinedServices.Add(serviceId);
        public void LeaveService(string serviceId) => JoinedServices.Remove(serviceId);
    }
}
