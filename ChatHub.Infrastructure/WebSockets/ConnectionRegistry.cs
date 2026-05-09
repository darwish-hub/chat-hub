using ChatHub.Core.Interfaces;
using System.Collections.Concurrent;

namespace ChatHub.Infrastructure.WebSockets;

public class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, IWebSocketConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _serviceIndex = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _userIndex = new();

    public void Register(string connectionId, IWebSocketConnection connection)
    {
        _connections[connectionId] = connection;
        _userIndex.AddOrUpdate(
            connection.UserId,
            _ => new ConcurrentDictionary<string, byte> { [connectionId] = 1 },
            (_, dict) => { dict[connectionId] = 1; return dict; }
        );
    }

    public void Unregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            // Remove from user index
            if (_userIndex.TryGetValue(connection.UserId, out var userConnections))
            {
                userConnections.TryRemove(connectionId, out _);
                if (userConnections.IsEmpty)
                    _userIndex.TryRemove(connection.UserId, out _);
            }

            // Remove from all services
            foreach (var serviceId in ((WebSocketConnection)connection).JoinedServices)
            {
                LeaveService(connectionId, serviceId);
            }
        }
    }

    public IWebSocketConnection? Get(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connection);
        return connection;
    }

    public IEnumerable<IWebSocketConnection> GetByService(string serviceId)
    {
        if (_serviceIndex.TryGetValue(serviceId, out var connectionIds))
        {
            foreach (var connectionId in connectionIds.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var connection))
                    yield return connection;
            }
        }
    }

    public IEnumerable<IWebSocketConnection> GetByUser(string userId)
    {
        if (_userIndex.TryGetValue(userId, out var connectionIds))
        {
            foreach (var connectionId in connectionIds.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var connection))
                    yield return connection;
            }
        }
    }

    public void JoinService(string connectionId, string serviceId)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.JoinService(serviceId);
            _serviceIndex.AddOrUpdate(
                serviceId,
                _ => new ConcurrentDictionary<string, byte> { [connectionId] = 1 },
                (_, dict) => { dict[connectionId] = 1; return dict; }
            );
        }
    }

    public void LeaveService(string connectionId, string serviceId)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.LeaveService(serviceId);
            if (_serviceIndex.TryGetValue(serviceId, out var serviceConnections))
            {
                serviceConnections.TryRemove(connectionId, out _);
                if (serviceConnections.IsEmpty)
                    _serviceIndex.TryRemove(serviceId, out _);
            }
        }
    }

    public IEnumerable<string> GetServiceConnectionIds(string serviceId)
    {
        if (_serviceIndex.TryGetValue(serviceId, out var connectionIds))
            return connectionIds.Keys.ToList();
        return Enumerable.Empty<string>();
    }
}
