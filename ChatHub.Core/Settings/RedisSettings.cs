namespace ChatHub.Core.Settings;

/// <summary>
/// Redis connection settings
/// </summary>
public class RedisSettings
{
    public const string SectionName = "Redis";
    
    public string ConnectionString { get; set; } = "localhost:6379";
}
