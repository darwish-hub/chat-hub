namespace ChatHub.Core.Interfaces;

public interface IConnectionRegistry
{
    void Register(string connectionId, IWebSocketConnection connection);
    void Unregister(string connectionId);
    IWebSocketConnection? Get(string connectionId);
    IEnumerable<IWebSocketConnection> GetByService(string serviceId);
    IEnumerable<IWebSocketConnection> GetByUser(string userId);
    void JoinService(string connectionId, string serviceId);
    void LeaveService(string connectionId, string serviceId);
    IEnumerable<string> GetServiceConnectionIds(string serviceId);
}
