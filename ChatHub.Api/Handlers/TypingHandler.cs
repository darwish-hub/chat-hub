using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Infrastructure.Cache;
using ChatHub.Infrastructure.WebSockets;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;

namespace ChatHub.Api.Handlers;

/// <summary>
/// Handles typing indicators with debouncing - broadcasts when user starts/stops typing
/// </summary>
public class TypingHandler : ITypingHandler
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IWebSocketSender _webSocketSender;
    private readonly INatsBackplane _natsBackplane;
    private readonly ILogger<TypingHandler> _logger;
    
    // Track typing state per connection with debounce timers
    private static readonly ConcurrentDictionary<string, TypingState> _typingStates = new();
    private const int TypingTimeoutMs = 3000; // 3 seconds of inactivity before "stopped typing"

    public TypingHandler(
        IConnectionRegistry connectionRegistry,
        IWebSocketSender webSocketSender,
        INatsBackplane natsBackplane,
        ILogger<TypingHandler> logger)
    {
        _connectionRegistry = connectionRegistry;
        _webSocketSender = webSocketSender;
        _natsBackplane = natsBackplane;
        _logger = logger;
    }

    public async Task HandleAsync(string connectionId, TypingMessage message, CancellationToken ct)
    {
        var connection = _connectionRegistry.Get(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found for typing indicator", connectionId);
            return;
        }

        var stateKey = $"{connectionId}:{message.ConversationId}";
        
        if (message.IsTyping)
        {
            // User started typing
            var existingState = _typingStates.GetOrAdd(stateKey, _key =>
            {
                // New typing session - broadcast immediately
                _ = BroadcastTypingAsync(connection, message.ConversationId, true, ct);
                
                return new TypingState
                {
                    IsTyping = true,
                    LastActivity = DateTime.UtcNow,
                    Timer = CreateDebounceTimer(stateKey, connection, message.ConversationId)
                };
            });

            if (existingState.IsTyping)
            {
                // Already typing - just update last activity and reset timer
                existingState.LastActivity = DateTime.UtcNow;
                existingState.Timer?.Stop();
                existingState.Timer?.Start();
            }
        }
        else
        {
            // User explicitly stopped typing
            if (_typingStates.TryRemove(stateKey, out var state))
            {
                state.Timer?.Stop();
                state.Timer?.Dispose();
                
                await BroadcastTypingAsync(connection, message.ConversationId, false, ct);
            }
        }
    }

    private System.Timers.Timer CreateDebounceTimer(string stateKey, IWebSocketConnection connection, string conversationId)
    {
        var timer = new System.Timers.Timer(TypingTimeoutMs);
        timer.Elapsed += async (sender, e) =>
        {
            // Check if still typing
            if (_typingStates.TryGetValue(stateKey, out var state))
            {
                var timeSinceLastActivity = DateTime.UtcNow - state.LastActivity;
                
                if (timeSinceLastActivity.TotalMilliseconds >= TypingTimeoutMs)
                {
                    // Timeout reached - mark as stopped typing
                    if (_typingStates.TryRemove(stateKey, out var removedState))
                    {
                        removedState.Timer?.Stop();
                        removedState.Timer?.Dispose();
                        
                        await BroadcastTypingAsync(connection, conversationId, false, CancellationToken.None);
                    }
                }
            }
        };
        timer.AutoReset = false;
        timer.Start();
        
        return timer;
    }

    private async Task BroadcastTypingAsync(IWebSocketConnection connection, string conversationId, bool isTyping, CancellationToken ct)
    {
        var typingIndicator = new TypingIndicator
        {
            UserId = connection.UserId,
            ConversationId = conversationId,
            IsTyping = isTyping
        };

        var json = JsonSerializer.Serialize(typingIndicator, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Broadcast to service connections
        var serviceId = connection.CurrentServiceId;
        if (!string.IsNullOrEmpty(serviceId))
        {
            var connections = _connectionRegistry.GetByService(serviceId);
            foreach (var conn in connections)
            {
                if (conn.ConnectionId != connection.ConnectionId)
                {
                    try
                    {
                        await _webSocketSender.SendTextAsync(conn.ConnectionId, bytes, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send typing indicator to connection {ConnectionId}", conn.ConnectionId);
                    }
                }
            }

            // Publish to NATS for cross-pod broadcast
            var subject = $"chathub.{serviceId}.presence";
            await _natsBackplane.PublishAsync(subject, bytes, ct);
        }

        _logger.LogDebug("Typing indicator broadcast: User {UserId} is {Status} in conversation {ConversationId}",
            connection.UserId, isTyping ? "typing" : "not typing", conversationId);
    }

    private class TypingState
    {
        public bool IsTyping { get; set; }
        public DateTime LastActivity { get; set; }
        public System.Timers.Timer? Timer { get; set; }
    }
}
