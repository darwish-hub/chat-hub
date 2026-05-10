using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatHub.Infrastructure.Cache;

/// <summary>
/// Background service that periodically cleans up abandoned in-memory voice sessions.
/// </summary>
public class VoiceSessionCleanupService : BackgroundService
{
    private readonly VoiceSessionBuffer _voiceBuffer;
    private readonly ILogger<VoiceSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);

    public VoiceSessionCleanupService(VoiceSessionBuffer voiceBuffer, ILogger<VoiceSessionCleanupService> logger)
    {
        _voiceBuffer = voiceBuffer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Voice session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cleaned = _voiceBuffer.CleanupExpiredSessions();
                if (cleaned > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired voice sessions", cleaned);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up voice sessions");
            }

            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Voice session cleanup service stopped");
    }
}
