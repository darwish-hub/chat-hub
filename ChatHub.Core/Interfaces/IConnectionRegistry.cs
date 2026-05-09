using System.Collections.Concurrent;
using System.Security.Claims;

namespace ChatHub.Core.Interfaces;

/// <summary>
/// Represents an active WebSocket connection
/// </summary>
public interface IWebSocketConnection
{
    string ConnectionId { get; }
    string UserId { get; }
    ClaimsPrincipal User { get; }
    DateTime ConnectedAt { get; }
    DateTime LastPongAt { get; }
    HashSet<string> JoinedServices { get; }
    
    void UpdateLastPong();
    void JoinService(string serviceId);
    void LeaveService(string serviceId);
}

/// <summary>
/// Thread-safe registry for managing active WebSocket connections
/// </summary>
public interface IConnectionRegistry
{
    /// <summary>
    /// All active connections by connection ID
    /// </summary>
    IReadOnlyDictionary<string, IWebSocketConnection> Connections { get; }
    
    /// <summary>
    /// Service index: serviceId -> set of connection IDs
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> ServiceIndex { get; }
    
    /// <summary>
    /// User index: userId -> set of connection IDs
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> UserIndex { get; }
    
    void Register(IWebSocketConnection connection);
    void Deregister(string connectionId);
    IWebSocketConnection? GetConnection(string connectionId);
    IReadOnlyCollection<IWebSocketConnection> GetConnectionsByService(string serviceId);
    IReadOnlyCollection<IWebSocketConnection> GetConnectionsByUser(string userId);
    void AddToService(string connectionId, string serviceId);
    void RemoveFromService(string connectionId, string serviceId);
}
