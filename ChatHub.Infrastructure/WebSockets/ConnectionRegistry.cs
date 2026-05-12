using ChatHub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ChatHub.Infrastructure.WebSockets;

public class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, IWebSocketConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _serviceIndex = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _userIndex = new();
    private readonly ILogger<ConnectionRegistry> _logger;

    public ConnectionRegistry(ILogger<ConnectionRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(string connectionId, IWebSocketConnection connection)
    {
        _logger.LogDebug("ConnectionRegistry: Register connection {ConnectionId} for user {UserId}", connectionId, connection.UserId);
        _connections[connectionId] = connection;
        _userIndex.AddOrUpdate(
            connection.UserId,
            _ => new ConcurrentDictionary<string, byte> { [connectionId] = 1 },
            (_, dict) => { dict[connectionId] = 1; return dict; }
        );
        _logger.LogDebug("ConnectionRegistry: Registered connection {ConnectionId}, total connections={Count}", connectionId, _connections.Count);
    }

    public void Unregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            _logger.LogDebug("ConnectionRegistry: Unregister connection {ConnectionId} for user {UserId}", connectionId, connection.UserId);
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
            _logger.LogDebug("ConnectionRegistry: Unregistered connection {ConnectionId}, total connections={Count}", connectionId, _connections.Count);
        }
        else
        {
            _logger.LogDebug("ConnectionRegistry: Unregister connection {ConnectionId} not found", connectionId);
        }
    }

    public IWebSocketConnection? Get(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connection);
        if (connection == null)
        {
            _logger.LogDebug("ConnectionRegistry: Get connection {ConnectionId} not found", connectionId);
        }
        return connection;
    }

    public IEnumerable<IWebSocketConnection> GetByService(string serviceId)
    {
        var count = 0;
        if (_serviceIndex.TryGetValue(serviceId, out var connectionIds))
        {
            foreach (var connectionId in connectionIds.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var connection))
                {
                    count++;
                    yield return connection;
                }
            }
        }
        _logger.LogDebug("ConnectionRegistry: GetByService {ServiceId} returned {Count} connections", serviceId, count);
    }

    public IEnumerable<IWebSocketConnection> GetByUser(string userId)
    {
        var count = 0;
        if (_userIndex.TryGetValue(userId, out var connectionIds))
        {
            foreach (var connectionId in connectionIds.Keys)
            {
                if (_connections.TryGetValue(connectionId, out var connection))
                {
                    count++;
                    yield return connection;
                }
            }
        }
        _logger.LogDebug("ConnectionRegistry: GetByUser {UserId} returned {Count} connections", userId, count);
    }

    public void JoinService(string connectionId, string serviceId)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            _logger.LogDebug("ConnectionRegistry: JoinService connection {ConnectionId} to service {ServiceId}", connectionId, serviceId);
            connection.JoinService(serviceId);
            _serviceIndex.AddOrUpdate(
                serviceId,
                _ => new ConcurrentDictionary<string, byte> { [connectionId] = 1 },
                (_, dict) => { dict[connectionId] = 1; return dict; }
            );
        }
        else
        {
            _logger.LogDebug("ConnectionRegistry: JoinService connection {ConnectionId} not found", connectionId);
        }
    }

    public void LeaveService(string connectionId, string serviceId)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            _logger.LogDebug("ConnectionRegistry: LeaveService connection {ConnectionId} from service {ServiceId}", connectionId, serviceId);
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
        {
            _logger.LogDebug("ConnectionRegistry: GetServiceConnectionIds {ServiceId} returned {Count} ids", serviceId, connectionIds.Count);
            return connectionIds.Keys.ToList();
        }
        _logger.LogDebug("ConnectionRegistry: GetServiceConnectionIds {ServiceId} returned empty", serviceId);
        return Enumerable.Empty<string>();
    }
}
