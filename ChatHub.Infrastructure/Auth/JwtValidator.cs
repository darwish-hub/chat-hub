using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChatHub.Infrastructure.Auth;

public class JwtValidator : IJwtValidator
{
    private readonly JwtSettings _settings;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<JwtValidator> _logger;

    public JwtValidator(
        IOptions<JwtSettings> settings,
        ILogger<JwtValidator> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();

        SecurityKey signingKey;

        if (string.Equals(_settings.Algorithm, "RS256", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_settings.PublicKey))
        {
            signingKey = LoadRsaPublicKey(_settings.PublicKey);
        }
        else
        {
            signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        }

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = _settings.ValidateAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        if (_settings.ValidateAudience)
        {
            _validationParameters.ValidAudience = _settings.Audience;
        }
    }

    public Task<JwtValidationResult> ValidateAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(token, _validationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken)
            {
                return Task.FromResult(JwtValidationResult.Failure("Invalid token type"));
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Task.FromResult(JwtValidationResult.Failure("Token missing user identifier"));
            }

            var expiresAt = jwtToken.ValidTo;

            return Task.FromResult(JwtValidationResult.Success(userId, expiresAt));
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult(JwtValidationResult.Failure("Token has expired"));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return Task.FromResult(JwtValidationResult.Failure("Invalid token signature"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");
            return Task.FromResult(JwtValidationResult.Failure("Token validation failed"));
        }
    }

    private static RsaSecurityKey LoadRsaPublicKey(string publicKeyPem)
    {
        var pem = publicKeyPem.Trim();
        if (pem.StartsWith("-----BEGIN PUBLIC KEY-----"))
        {
            pem = pem
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();
        }

        var keyBytes = Convert.FromBase64String(pem);

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

        return new RsaSecurityKey(rsa.ExportParameters(false))
        {
            KeyId = "anat-rsa"
        };
    }
}