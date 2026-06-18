namespace Store.Application.Auth;

/// <summary>Configuration for issuing/validating JWT access tokens (bound from the <c>Jwt</c> config section).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MyStore";

    public string Audience { get; set; } = "MyStore";

    /// <summary>Symmetric signing key. Must be at least 32 bytes for HMAC-SHA256.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>Lifetime (in days) of the rotating refresh token carried in the httpOnly cookie.</summary>
    public int RefreshTokenDays { get; set; } = 14;
}
