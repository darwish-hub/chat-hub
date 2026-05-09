namespace ChatHub.Core.Settings;

public class NatsSettings
{
    public string Url { get; set; } = "nats://localhost:4222";
    public string QueueGroup { get; set; } = "chathub-hub";
}
