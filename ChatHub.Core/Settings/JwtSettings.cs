namespace ChatHub.Core.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "chathub";
    public string Audience { get; set; } = "chathub-clients";
    public bool ValidateAudience { get; set; } = true;

    public string PublicKey { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "HS256";
}
