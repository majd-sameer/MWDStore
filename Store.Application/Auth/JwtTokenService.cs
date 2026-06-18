using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Store.Application.Auth;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs containing the user's id (<c>sub</c>), name, email and role claims.
/// The clock is injected via <see cref="TimeProvider"/> so token lifetimes are deterministic in tests.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(JwtOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public AccessToken CreateToken(long userId, string? userName, string? email, IEnumerable<string> roles)
    {
        var now = _timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(userName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, userName));
        }

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(tokenString, expires);
    }
}
