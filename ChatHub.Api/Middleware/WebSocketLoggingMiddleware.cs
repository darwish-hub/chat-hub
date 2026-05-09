using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace ChatHub.Api.Middleware;

public class WebSocketLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketLoggingMiddleware> _logger;

    public WebSocketLoggingMiddleware(RequestDelegate next, ILogger<WebSocketLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var connectionId = context.Connection.Id;
        var userId = context.User.Identity?.Name ?? "anonymous";
        var correlationId = Guid.NewGuid().ToString("N");

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ConnectionId"] = connectionId,
            ["UserId"] = userId
        }))
        {
            _logger.LogInformation("WebSocket connection established - ConnectionId: {ConnectionId}, User: {UserId}",
                connectionId, userId);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogWarning("WebSocket connection closed prematurely - ConnectionId: {ConnectionId}",
                    connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket error - ConnectionId: {ConnectionId}", connectionId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "WebSocket connection closed - ConnectionId: {ConnectionId}, Duration: {DurationMs}ms",
                    connectionId, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
