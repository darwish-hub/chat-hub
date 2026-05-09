namespace ChatHub.Core.Settings;

/// <summary>
/// Main application settings
/// </summary>
public class ChatHubSettings
{
    public const string SectionName = "ChatHub";
    
    public int MaxMessageSizeBytes { get; set; } = 65536; // 64KB
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public int PingIntervalSeconds { get; set; } = 15;
    public int PongTimeoutSeconds { get; set; } = 10;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int RateLimitTextPerMinute { get; set; } = 100;
    public int RateLimitVoicePerMinute { get; set; } = 10;
    public string PodId { get; set; } = Environment.MachineName;
}
