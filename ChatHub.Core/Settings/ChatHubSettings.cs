namespace ChatHub.Core.Settings;

public class ChatHubSettings
{
    public int MaxMessageSizeBytes { get; set; } = 65536;
    public int PingIntervalSeconds { get; set; } = 15;
    public int PongTimeoutSeconds { get; set; } = 10;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int RateLimitTextPerMinute { get; set; } = 100;
    public int RateLimitVoicePerMinute { get; set; } = 10;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public string PodId { get; set; } = "unknown";
}
