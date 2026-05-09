namespace ChatHub.Core.Settings;

/// <summary>
/// NATS connection settings
/// </summary>
public class NatsSettings
{
    public const string SectionName = "Nats";
    
    public string Url { get; set; } = "nats://localhost:4222";
    public string QueueGroup { get; set; } = "chathub-hub";
}
