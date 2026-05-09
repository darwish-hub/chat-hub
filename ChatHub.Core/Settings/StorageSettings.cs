namespace ChatHub.Core.Settings;

/// <summary>
/// S3-compatible storage settings
/// </summary>
public class StorageSettings
{
    public const string SectionName = "Storage";
    
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "chathub";
    public bool UseSsl { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
}
