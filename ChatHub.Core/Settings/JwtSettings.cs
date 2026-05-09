namespace ChatHub.Core.Settings;

/// <summary>
/// JWT validation settings
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";
    
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "chathub";
    public string Audience { get; set; } = "chathub-clients";
}
