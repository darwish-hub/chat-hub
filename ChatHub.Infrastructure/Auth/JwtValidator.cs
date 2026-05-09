using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatHub.Core.Interfaces;
using ChatHub.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChatHub.Infrastructure.Auth;

/// <summary>
/// JWT token validator
/// </summary>
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
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
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
}
