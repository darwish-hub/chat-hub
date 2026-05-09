namespace ChatHub.Core.Settings;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/chathub";
    public string DatabaseName { get; set; } = "chathub";
}
