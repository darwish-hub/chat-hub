using System.Buffers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.WebSockets;
using Microsoft.Extensions.Options;

namespace ChatHub.Api.Middleware;

/// <summary>
/// WebSocket middleware that handles connection acceptance, receive loop, and heartbeat
/// </summary>
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ChatHubSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public WebSocketMiddleware(
        RequestDelegate next,
        ILogger<WebSocketMiddleware> logger,
        IServiceProvider serviceProvider,
        IOptions<ChatHubSettings> settings)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }
        
        // Extract and validate JWT from query parameter
        var token = context.Request.Query["token"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        
        using var scope = _serviceProvider.CreateScope();
        var jwtValidator = scope.ServiceProvider.GetRequiredService<IJwtValidator>();
        
        var validationResult = await jwtValidator.ValidateAsync(token);
        
        if (!validationResult.IsValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        
        // Accept WebSocket connection
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();
        
        // Create ClaimsPrincipal
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, validationResult.UserId!)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);
        
        // Create connection record
        var connection = new WebSocketConnection(
            connectionId,
            validationResult.UserId!,
            principal,
            webSocket);
        
        // Register connection
        var registry = scope.ServiceProvider.GetRequiredService<IConnectionRegistry>();
        registry.Register(connection);
        
        _logger.LogInformation(
            "WebSocket connection {ConnectionId} established for user {UserId}",
            connectionId,
            validationResult.UserId);
        
        try
        {
            // Run receive and heartbeat loops concurrently
            var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            
            await Task.WhenAll(
                ReceiveLoopAsync(connection, dispatcher, connection.ConnectionToken),
                HeartbeatLoopAsync(connection, validationResult.ExpiresAt!.Value));
        }
        catch (OperationCanceledException)
        {
            // Expected on connection close
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket connection {ConnectionId}", connectionId);
        }
        finally
        {
            // Cleanup
            registry.Deregister(connectionId);
            await connection.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Connection closed");
            connection.Dispose();
            
            _logger.LogInformation(
                "WebSocket connection {ConnectionId} closed",
                connectionId);
        }
    }
    
    private async Task ReceiveLoopAsync(
        WebSocketConnection connection,
        IMessageDispatcher dispatcher,
        CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        
        try
        {
            while (!ct.IsCancellationRequested && connection.WebSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                
                do
                {
                    result = await connection.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Handle binary voice chunk
                        await HandleBinaryChunkAsync(connection, buffer.AsMemory(0, result.Count));
                        continue;
                    }
                    
                    await ms.WriteAsync(buffer.AsMemory(0, result.Count), ct);
                } while (!result.EndOfMessage);
                
                if (ms.Length > 0)
                {
                    ms.Position = 0;
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    
                    try
                    {
                        var message = JsonSerializer.Deserialize<ClientMessage>(json, JsonOptions);
                        
                        if (message != null)
                        {
                            await dispatcher.DispatchAsync(connection.ConnectionId, message, ct);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize message from {ConnectionId}",
                            connection.ConnectionId);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    private async Task HandleBinaryChunkAsync(WebSocketConnection connection, ReadOnlyMemory<byte> data)
    {
        // Extract connectionId (36 chars) and conversationId (36 chars) from first 72 bytes
        if (data.Length < 72)
        {
            _logger.LogWarning("Binary chunk too small from {ConnectionId}", connection.ConnectionId);
            return;
        }
        
        var header = Encoding.ASCII.GetString(data.Slice(0, 72).Span);
        var targetConnectionId = header.Substring(0, 36);
        var conversationId = header.Substring(36, 36);
        var payload = data.Slice(72);
        
        // Forward voice chunk to target
        // This is a simplified implementation - in reality, we'd need proper voice chunk routing
        _logger.LogDebug("Received voice chunk for conversation {ConversationId}", conversationId);
    }
    
    private async Task HeartbeatLoopAsync(WebSocketConnection connection, DateTime tokenExpiresAt)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.PingIntervalSeconds));
        
        while (await timer.WaitForNextTickAsync(connection.ConnectionToken))
        {
            // Check token expiry
            if (DateTime.UtcNow >= tokenExpiresAt)
            {
                _logger.LogInformation("Token expired for connection {ConnectionId}",
                    connection.ConnectionId);
                connection.Abort();
                return;
            }
            
            // Check idle timeout
            var idleTime = DateTime.UtcNow - connection.LastPongAt;
            if (idleTime > TimeSpan.FromMinutes(_settings.IdleTimeoutMinutes))
            {
                _logger.LogInformation("Connection {ConnectionId} idle timeout",
                    connection.ConnectionId);
                connection.Abort();
                return;
            }
            
            // Check pong timeout
            var pongTime = DateTime.UtcNow - connection.LastPongAt;
            if (pongTime > TimeSpan.FromSeconds(_settings.PongTimeoutSeconds + _settings.PingIntervalSeconds))
            {
                _logger.LogWarning("Pong timeout for connection {ConnectionId}",
                    connection.ConnectionId);
                connection.Abort();
                return;
            }
            
            // Send ping
            var pingMessage = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
            connection.QueueSend(pingMessage, WebSocketMessageType.Text);
        }
    }
}

// Extension method to add the middleware
public static class WebSocketMiddlewareExtensions
{
    public static IApplicationBuilder UseChatHubWebSockets(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSocketMiddleware>();
    }
}
