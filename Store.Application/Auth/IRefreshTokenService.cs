namespace Store.Application.Auth;

/// <summary>
/// A freshly-issued refresh token: the <see cref="RawToken"/> goes to the client in the
/// httpOnly cookie, while only its <see cref="Hash"/> is persisted server-side (so a database
/// leak never exposes a usable token). Both expire at <see cref="ExpiresAt"/>.
/// </summary>
public sealed record RefreshToken(string RawToken, string Hash, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues and verifies rotating refresh tokens. Tokens are cryptographically-random opaque
/// strings; the server stores only a SHA-256 hash and compares in constant time.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Generates a new random refresh token together with its storable hash and expiry.</summary>
    RefreshToken Issue();

    /// <summary>Deterministic SHA-256 hash (Base64) of a raw token, used to look up the owning user.</summary>
    string Hash(string rawToken);

    /// <summary>Constant-time comparison of a presented raw token against a stored hash.</summary>
    bool Matches(string rawToken, string? storedHash);

    /// <summary>Configured refresh-token lifetime.</summary>
    TimeSpan Lifetime { get; }
}
