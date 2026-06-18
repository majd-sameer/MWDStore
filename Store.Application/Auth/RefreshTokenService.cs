using System.Security.Cryptography;
using System.Text;

namespace Store.Application.Auth;

/// <summary>
/// Default <see cref="IRefreshTokenService"/>: 256 bits of CSPRNG entropy per token, hashed with
/// SHA-256 for storage. The clock comes from <see cref="TimeProvider"/> so expiry is deterministic
/// in tests.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int TokenByteLength = 32;

    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenService(JwtOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public TimeSpan Lifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public RefreshToken Issue()
    {
        // Lowercase hex keeps the value safe to drop straight into a Set-Cookie header.
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenByteLength)).ToLowerInvariant();
        return new RefreshToken(raw, Hash(raw), _timeProvider.GetUtcNow().Add(Lifetime));
    }

    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    public bool Matches(string rawToken, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(Hash(rawToken));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }
}
