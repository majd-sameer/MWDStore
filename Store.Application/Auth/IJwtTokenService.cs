namespace Store.Application.Auth;

/// <summary>An issued access token and its absolute expiry.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    AccessToken CreateToken(long userId, string? userName, string? email, IEnumerable<string> roles);
}
