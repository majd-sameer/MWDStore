using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Store.Application.Auth;

namespace Store.Application.Tests;

/// <summary>
/// Verifies the issued JWT validates against the configured key/issuer/audience and carries the expected
/// subject, email and role claims.
/// </summary>
public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "MyStore",
        Audience = "MyStoreClients",
        Key = "unit-test-signing-key-which-is-long-enough-32+chars",
        ExpiryMinutes = 60
    };

    [Fact]
    public void CreateToken_IssuesValidToken_WithExpectedClaims()
    {
        var service = new JwtTokenService(Options, new FixedTimeProvider(Now));

        var token = service.CreateToken(42, "alice", "alice@example.com", ["Admin", "Customer"]);

        Assert.Equal(Now.AddMinutes(60), token.ExpiresAt);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Options.Issuer,
            ValidateAudience = true,
            ValidAudience = Options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Key)),
            ValidateLifetime = false
        };

        var handler = new JwtSecurityTokenHandler();
        // ValidateToken throws if the signature/issuer/audience are invalid.
        handler.ValidateToken(token.Token, validationParameters, out var validated);
        var jwt = (JwtSecurityToken)validated;

        Assert.Equal("42", jwt.Subject);
        Assert.Equal("MyStore", jwt.Issuer);
        Assert.Contains("MyStoreClients", jwt.Audiences);
        Assert.Equal("alice@example.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal(["Admin", "Customer"], roles);
    }
}
