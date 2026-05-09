using System.Collections.Concurrent;
using ChatHub.Core.Interfaces;

namespace ChatHub.Infrastructure.WebSockets;

/// <summary>
/// Thread-safe registry for managing active WebSocket connections
/// </summary>
public class ConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, IWebSocketConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _serviceIndex = new();
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _userIndex = new();
    
    public IReadOnlyDictionary<string, IWebSocketConnection> Connections => _connections;
    
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ServiceIndex =>
        _serviceIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<string>)kvp.Value);
    
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> UserIndex =>
        _userIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<string>)kvp.Value);
    
    public void Register(IWebSocketConnection connection)
    {
        _connections[connection.ConnectionId] = connection;
        
        _userIndex.AddOrUpdate(
            connection.UserId,
            _ => new ConcurrentHashSet<string> { connection.ConnectionId },
            (_, set) =>
            {
                set.Add(connection.ConnectionId);
                return set;
            });
    }
    
    public void Deregister(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            // Remove from user index
            if (_userIndex.TryGetValue(connection.UserId, out var userConnections))
            {
                userConnections.TryRemove(connectionId);
                if (userConnections.IsEmpty)
                {
                    _userIndex.TryRemove(connection.UserId, out _);
                }
            }
            
            // Remove from all service indices
            foreach (var serviceId in connection.JoinedServices)
            {
                RemoveFromService(connectionId, serviceId);
            }
        }
    }
    
    public IWebSocketConnection? GetConnection(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connection);
        return connection;
    }
    
    public IReadOnlyCollection<IWebSocketConnection> GetConnectionsByService(string serviceId)
    {
        if (_serviceIndex.TryGetValue(serviceId, out var connectionIds))
        {
            return connectionIds
                .Select(id => _connections.TryGetValue(id, out var conn) ? conn : null)
                .Where(c => c != null)
                .Cast<IWebSocketConnection>()
                .ToList();
        }
        return Array.Empty<IWebSocketConnection>();
    }
    
    public IReadOnlyCollection<IWebSocketConnection> GetConnectionsByUser(string userId)
    {
        if (_userIndex.TryGetValue(userId, out var connectionIds))
        {
            return connectionIds
                .Select(id => _connections.TryGetValue(id, out var conn) ? conn : null)
                .Where(c => c != null)
                .Cast<IWebSocketConnection>()
                .ToList();
        }
        return Array.Empty<IWebSocketConnection>();
    }
    
    public void AddToService(string connectionId, string serviceId)
    {
        _serviceIndex.AddOrUpdate(
            serviceId,
            _ => new ConcurrentHashSet<string> { connectionId },
            (_, set) =>
            {
                set.Add(connectionId);
                return set;
            });
        
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.JoinService(serviceId);
        }
    }
    
    public void RemoveFromService(string connectionId, string serviceId)
    {
        if (_serviceIndex.TryGetValue(serviceId, out var connections))
        {
            connections.TryRemove(connectionId);
            if (connections.IsEmpty)
            {
                _serviceIndex.TryRemove(serviceId, out _);
            }
        }
        
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.LeaveService(serviceId);
        }
    }
}

/// <summary>
/// Thread-safe hash set implementation
/// </summary>
public class ConcurrentHashSet<T> : IReadOnlyCollection<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();
    
    public int Count => _dictionary.Count;
    public bool IsEmpty => _dictionary.IsEmpty;
    
    public bool Add(T item) => _dictionary.TryAdd(item, 0);
    public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);
    public bool Contains(T item) => _dictionary.ContainsKey(item);
    
    public IEnumerator<T> GetEnumerator() => _dictionary.Keys.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
