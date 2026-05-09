using System.Diagnostics;

namespace ChatHub.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            return Task.CompletedTask;
        });

        // Create a scope with the correlation ID for all logs in this request
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method
        }))
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Request started {Method} {Path}", 
                    context.Request.Method, 
                    context.Request.Path);
                
                await _next(context);
                
                stopwatch.Stop();
                _logger.LogInformation(
                    "Request completed {Method} {Path} - Status {StatusCode} in {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "Request failed {Method} {Path} - {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
