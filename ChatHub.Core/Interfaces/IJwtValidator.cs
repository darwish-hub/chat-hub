namespace ChatHub.Core.Interfaces;

/// <summary>
/// JWT validation service
/// </summary>
public interface IJwtValidator
{
    /// <summary>
    /// Validate a JWT token and return the user ID if valid
    /// </summary>
    Task<JwtValidationResult> ValidateAsync(string token, CancellationToken ct = default);
}

public record JwtValidationResult
{
    public bool IsValid { get; init; }
    public string? UserId { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? Error { get; init; }
    
    public static JwtValidationResult Success(string userId, DateTime expiresAt) =>
        new() { IsValid = true, UserId = userId, ExpiresAt = expiresAt };
    
    public static JwtValidationResult Failure(string error) =>
        new() { IsValid = false, Error = error };
}
