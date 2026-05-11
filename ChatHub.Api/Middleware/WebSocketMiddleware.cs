using ChatHub.Api.Handlers;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Models;
using ChatHub.Core.Settings;
using ChatHub.Infrastructure.WebSockets;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ChatHub.Api.Middleware;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IWebSocketSender _webSocketSender;
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly IJwtValidator _jwtValidator;
    private readonly ChatHubSettings _settings;

    public WebSocketMiddleware(
        RequestDelegate next,
        ILogger<WebSocketMiddleware> logger,
        IConnectionRegistry connectionRegistry,
        IWebSocketSender webSocketSender,
        IMessageDispatcher messageDispatcher,
        IServiceProvider serviceProvider,
        IJwtValidator jwtValidator,
        IOptions<ChatHubSettings> settings)
    {
        _next = next;
        _logger = logger;
        _connectionRegistry = connectionRegistry;
        _webSocketSender = webSocketSender;
        _messageDispatcher = messageDispatcher;
        _serviceProvider = serviceProvider;
        _jwtValidator = jwtValidator;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        // Validate JWT from query parameter
        if (!await ValidateTokenAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = context.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var connectionId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("WebSocket connection request from user {UserId}, connection {ConnectionId}", userId, connectionId);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        
        var connection = new WebSocketConnection(
            connectionId,
            userId,
            user,
            webSocket,
            cts
        );

        _connectionRegistry.Register(connectionId, connection);

        try
        {
            // Run receive and heartbeat loops concurrently
            var receiveTask = ReceiveLoopAsync(connection);
            var heartbeatTask = HeartbeatLoopAsync(connection);
            
            await Task.WhenAll(receiveTask, heartbeatTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error for connection {ConnectionId}", connectionId);
        }
        finally
        {
            _connectionRegistry.Unregister(connectionId);
            (_webSocketSender as IDisposable)?.Dispose();
            
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            
            _logger.LogInformation("WebSocket connection {ConnectionId} closed", connectionId);
        }
    }

    private async Task<bool> ValidateTokenAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        var token = context.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var result = await _jwtValidator.ValidateAsync(token);
        if (!result.IsValid)
        {
            _logger.LogWarning("JWT validation failed: {Error}", result.Error);
            return false;
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.UserId!),
            new Claim("sub", result.UserId!)
        };
        var identity = new ClaimsIdentity(claims, "JwtBearer");
        context.User = new ClaimsPrincipal(identity);
        return true;
    }

    private async Task ReceiveLoopAsync(WebSocketConnection connection)
    {
        var buffer = new byte[_settings.MaxMessageSizeBytes];
        
        try
        {
            while (connection.WebSocket.State == WebSocketState.Open && !connection.Cts.Token.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                
                do
                {
                    result = await connection.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), connection.Cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        ms.Write(buffer, 0, result.Count);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Handle binary data (voice chunks)
                        await HandleBinaryDataAsync(connection.ConnectionId, buffer.AsMemory(0, result.Count));
                    }
                }
                while (!result.EndOfMessage);

                if (ms.Length > 0)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    await HandleTextMessageAsync(connection.ConnectionId, json);
                }
            }
        }
        catch (WebSocketException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in receive loop for connection {ConnectionId}", connection.ConnectionId);
        }
    }

    private async Task HandleTextMessageAsync(string connectionId, string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ClientMessage>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (message != null)
            {
                await _messageDispatcher.DispatchAsync(connectionId, message, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error despatching message from connection {ConnectionId}", connectionId);
        }
    }

    private async Task HandleBinaryDataAsync(string connectionId, ReadOnlyMemory<byte> data)
    {
        // Handle binary data (voice chunks)
        // Get VoiceChunkHandler from service provider
        var voiceChunkHandler = _serviceProvider.GetService(typeof(VoiceChunkHandler)) as VoiceChunkHandler;
        
        if (voiceChunkHandler != null)
        {
            await voiceChunkHandler.HandleBinaryDataAsync(connectionId, data.ToArray(), CancellationToken.None);
        }
        else
        {
            _logger.LogDebug("Binary data received for connection {ConnectionId}, size: {Size} bytes - no handler", 
                connectionId, data.Length);
        }
    }

    private async Task HeartbeatLoopAsync(WebSocketConnection connection)
    {
        try
        {
            while (connection.WebSocket.State == WebSocketState.Open && !connection.Cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.PingIntervalSeconds), connection.Cts.Token);

                // Send ping
                var pingMessage = JsonSerializer.Serialize(new { type = "ping" });
                var pingData = Encoding.UTF8.GetBytes(pingMessage);
                
                await connection.WebSocket.SendAsync(
                    new ArraySegment<byte>(pingData),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    connection.Cts.Token);

                // Check for pong timeout
                var timeSinceLastPong = DateTime.UtcNow - connection.LastPongAt;
                if (timeSinceLastPong > TimeSpan.FromSeconds(_settings.PongTimeoutSeconds + _settings.PingIntervalSeconds))
                {
                    _logger.LogWarning("Pong timeout for connection {ConnectionId}", connection.ConnectionId);
                    connection.Cts.Cancel();
                    break;
                }

                // Check idle timeout
                var idleTime = DateTime.UtcNow - connection.LastPongAt;
                if (idleTime > TimeSpan.FromMinutes(_settings.IdleTimeoutMinutes))
                {
                    _logger.LogInformation("Idle timeout for connection {ConnectionId}", connection.ConnectionId);
                    connection.Cts.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection closes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in heartbeat loop for connection {ConnectionId}", connection.ConnectionId);
        }
    }
}

public static class WebSocketMiddlewareExtensions
{
    public static IApplicationBuilder UseChatHubWebSockets(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<WebSocketMiddleware>();
    }
}
