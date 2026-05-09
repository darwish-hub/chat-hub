namespace ChatHub.Core.Settings;

public class StorageSettings
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "chathub-uploads";
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
}
