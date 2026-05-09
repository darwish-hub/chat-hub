using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace ChatHub.Api.Metrics;

public class ChatMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _connectionsEstablished;
    private readonly Counter<long> _connectionsClosed;
    private readonly Histogram<double> _messageLatency;
    private readonly Histogram<double> _connectionDuration;
    private readonly ConcurrentDictionary<string, DateTime> _connectionStartTimes = new();

    public ChatMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("ChatHub");
        
        _messagesSent = _meter.CreateCounter<long>(
            "chathub.messages.sent",
            "messages",
            "Number of messages sent by users");
        
        _messagesReceived = _meter.CreateCounter<long>(
            "chathub.messages.received",
            "messages",
            "Number of messages received by users");
        
        _connectionsEstablished = _meter.CreateCounter<long>(
            "chathub.connections.established",
            "connections",
            "Number of WebSocket connections established");
        
        _connectionsClosed = _meter.CreateCounter<long>(
            "chathub.connections.closed",
            "connections",
            "Number of WebSocket connections closed");
        
        _messageLatency = _meter.CreateHistogram<double>(
            "chathub.messages.latency",
            "milliseconds",
            "Message delivery latency in milliseconds");
        
        _connectionDuration = _meter.CreateHistogram<double>(
            "chathub.connections.duration",
            "seconds",
            "WebSocket connection duration in seconds");
    }

    public void RecordMessageSent(string messageType, string conversationId)
    {
        _messagesSent.Add(1, 
            new KeyValuePair<string, object?>("message.type", messageType),
            new KeyValuePair<string, object?>("conversation.id", conversationId));
    }

    public void RecordMessageReceived(string messageType, string conversationId)
    {
        _messagesReceived.Add(1,
            new KeyValuePair<string, object?>("message.type", messageType),
            new KeyValuePair<string, object?>("conversation.id", conversationId));
    }

    public void RecordMessageLatency(string messageType, TimeSpan latency)
    {
        _messageLatency.Record(latency.TotalMilliseconds,
            new KeyValuePair<string, object?>("message.type", messageType));
    }

    public void RecordConnectionEstablished(string connectionId)
    {
        _connectionsEstablished.Add(1);
        _connectionStartTimes[connectionId] = DateTime.UtcNow;
    }

    public void RecordConnectionClosed(string connectionId)
    {
        _connectionsClosed.Add(1);
        
        if (_connectionStartTimes.TryRemove(connectionId, out var startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            _connectionDuration.Record(duration.TotalSeconds);
        }
    }
}
