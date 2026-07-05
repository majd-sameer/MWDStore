using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Store.Application.Auth;

/// <summary>
/// Default <see cref="IMfaChallengeService"/>. The challenge is a JWT signed with the SAME HMAC key as the
/// access token, but deliberately scoped to a DISTINCT audience (<see cref="ChallengeAudience"/>) and stamped
/// with a <c>token_use=mfa_challenge</c> claim.
///
/// Why it can NEVER be accepted as an access token: the JwtBearer handler in Program.cs validates with
/// <c>ValidateAudience = true</c> and <c>ValidAudience = jwtOptions.Audience</c> (the access-token audience).
/// A challenge carries a different audience, so the bearer pipeline's audience check fails and the token is
/// rejected before any endpoint sees it (401). The <c>token_use</c> claim is a second, independent gate.
///
/// The challenge is intentionally minimal — it carries the user id and nothing else (no roles, name or email),
/// so even if it were somehow presented to an authorization layer it grants no identity beyond the subject.
/// </summary>
public sealed class MfaChallengeService : IMfaChallengeService
{
    /// <summary>
    /// How long a challenge stays valid. Deliberately short: a stateless JWT cannot be revoked, so single-use
    /// can't be enforced server-side. The tight TTL plus Identity lockout on failed codes are the mitigations
    /// (a captured challenge is only replayable within this window, and only against a non-locked account).
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public const string TokenUseClaim = "token_use";
    public const string TokenUseValue = "mfa_challenge";

    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public MfaChallengeService(JwtOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>The audience challenges are minted for — intentionally different from the access-token audience.</summary>
    public string ChallengeAudience => _options.Audience + ":mfa-challenge";

    public MfaChallenge Create(long userId)
    {
        var now = _timeProvider.GetUtcNow();
        var expires = now.Add(Lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenUseClaim, TokenUseValue)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: ChallengeAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new MfaChallenge(tokenString, expires);
    }

    public long? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = ChallengeAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);

            // Defence in depth: reject anything not explicitly minted as a challenge, even if the audience matched.
            if (principal.FindFirstValue(TokenUseClaim) != TokenUseValue)
            {
                return null;
            }

            // The handler remaps "sub" to NameIdentifier on the inbound principal; accept either.
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return long.TryParse(sub, out var userId) ? userId : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            // Bad signature, wrong audience/issuer, expired, or malformed — all resolve to "no valid challenge".
            return null;
        }
    }
}
