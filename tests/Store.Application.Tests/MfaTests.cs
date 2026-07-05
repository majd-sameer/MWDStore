using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Store.Api.Controllers;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auth;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// TOTP multi-factor authentication: enrolment (setup/enable), the login challenge + verify flow, recovery
/// codes, disable, and the lockout/brute-force guards. Everything runs against EF InMemory + a real
/// <see cref="UserManager{TUser}"/> wired exactly like Store.Api's Program.cs (AddIdentityCore +
/// AddEntityFrameworkStores + AddDefaultTokenProviders), so the authenticator/recovery-code token stores
/// behave as they do at runtime. TOTP codes are computed from the shared secret with a local RFC-6238
/// implementation (the built-in AuthenticatorTokenProvider does not generate codes, only validates them).
/// </summary>
public class MfaTests
{
    private static readonly JwtOptions JwtOptionsValue = new()
    {
        Issuer = "MyStore",
        Audience = "MyStore",
        Key = "unit-test-signing-key-which-is-long-enough-32+chars",
        ExpiryMinutes = 60,
        RefreshTokenDays = 14
    };

    // ----- Wiring helpers ------------------------------------------------------------------------------

    private static UserManager<User> NewUserManager(StoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddDataProtection();
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredUniqueChars = 0;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<StoreDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }

    private static MfaChallengeService NewChallengeService(TimeProvider timeProvider) =>
        new(JwtOptionsValue, timeProvider);

    private static AuthController NewController(
        UserManager<User> userManager, IMfaChallengeService mfaService, TimeProvider timeProvider,
        User? authenticatedUser = null)
    {
        var controller = new AuthController(
            userManager,
            new JwtTokenService(JwtOptionsValue, timeProvider),
            new RefreshTokenService(JwtOptionsValue, timeProvider),
            new FakePasswordResetService(),
            new FakeWelcomeEmailService(),
            mfaService,
            new FakeAntiforgery(),
            timeProvider);

        var httpContext = new DefaultHttpContext();
        if (authenticatedUser is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, authenticatedUser.Id.ToString(CultureInfo.InvariantCulture))],
                "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static async Task<User> CreateUserAsync(
        UserManager<User> userManager, string email = "mfa@example.com", string password = "Pass@1234")
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            FullName = "MFA User",
            UserGuid = Guid.NewGuid(),
            CreatedOn = DateTimeOffset.UtcNow,
            LatestUpdatedOn = DateTimeOffset.UtcNow
        };
        Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
        return user;
    }

    /// <summary>Runs setup + enable through the controller and returns the raw shared secret + recovery codes.</summary>
    private static async Task<(string RawKey, IReadOnlyList<string> RecoveryCodes)> EnrollAsync(
        UserManager<User> userManager, User user)
    {
        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System, user);

        Assert.IsType<OkObjectResult>((await controller.MfaSetup()).Result);
        var rawKey = await userManager.GetAuthenticatorKeyAsync(user);
        Assert.False(string.IsNullOrEmpty(rawKey));

        var enable = await controller.MfaEnable(
            new MfaEnableRequest { Code = ComputeTotp(rawKey!, DateTimeOffset.UtcNow) });
        var body = Assert.IsType<MfaEnableResponse>(Assert.IsType<OkObjectResult>(enable.Result).Value);
        return (rawKey!, body.RecoveryCodes);
    }

    // ----- Enrolment -----------------------------------------------------------------------------------

    [Fact]
    public async Task Setup_ResetsKey_AndReturnsFormattedSecretPlusOtpauthUri()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System, user);
        var body = Assert.IsType<MfaSetupResponse>(Assert.IsType<OkObjectResult>((await controller.MfaSetup()).Result).Value);

        Assert.False(string.IsNullOrWhiteSpace(body.SharedKey));
        var rawKey = await userManager.GetAuthenticatorKeyAsync(user);
        Assert.StartsWith("otpauth://totp/MyStore:mfa%40example.com?", body.AuthenticatorUri);
        Assert.Contains($"secret={rawKey}", body.AuthenticatorUri);
        Assert.Contains("issuer=MyStore", body.AuthenticatorUri);
    }

    [Fact]
    public async Task Enable_WithValidCode_EnablesTwoFactor_AndReturnsTenRecoveryCodes()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);

        var (_, recoveryCodes) = await EnrollAsync(userManager, user);

        Assert.True(await userManager.GetTwoFactorEnabledAsync(user));
        Assert.Equal(10, recoveryCodes.Count);
        Assert.Equal(10, recoveryCodes.Distinct().Count());
    }

    [Fact]
    public async Task Enable_WithBadCode_IsRejected_AndTwoFactorStaysOff()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System, user);
        Assert.IsType<OkObjectResult>((await controller.MfaSetup()).Result);

        var result = await controller.MfaEnable(new MfaEnableRequest { Code = "000000" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(await userManager.GetTwoFactorEnabledAsync(user));
    }

    // ----- Login challenge -----------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithMfaEnabled_ReturnsChallenge_WithoutIssuingTokens()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        await EnrollAsync(userManager, user);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System);
        var result = await controller.Login(new LoginRequest { Email = user.Email!, Password = "Pass@1234" });

        var body = Assert.IsType<MfaChallengeResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(body.MfaRequired);
        Assert.False(string.IsNullOrEmpty(body.ChallengeToken));
        // No tokens leaked: the challenge body carries no access token and no refresh cookie is set.
        Assert.DoesNotContain(controller.Response.Headers.SetCookie, h => h!.Contains(AuthCookies.RefreshToken));
    }

    [Fact]
    public async Task Login_WithoutMfa_IssuesTokensAsBefore()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System);
        var result = await controller.Login(new LoginRequest { Email = user.Email!, Password = "Pass@1234" });

        Assert.IsType<AuthResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Contains(controller.Response.Headers.SetCookie, h => h!.Contains(AuthCookies.RefreshToken));
    }

    [Fact]
    public async Task MfaVerify_WithValidTotp_IssuesTokens()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        var (rawKey, _) = await EnrollAsync(userManager, user);

        var challenge = NewChallengeService(TimeProvider.System).Create(user.Id);
        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System);

        var result = await controller.MfaVerify(new MfaVerifyRequest
        {
            ChallengeToken = challenge.Token,
            Code = ComputeTotp(rawKey, DateTimeOffset.UtcNow)
        });

        var body = Assert.IsType<AuthResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(user.Id, body.UserId);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.Contains(controller.Response.Headers.SetCookie, h => h!.Contains(AuthCookies.RefreshToken));
    }

    [Fact]
    public async Task MfaVerify_WithGarbageOrExpiredChallenge_IsRejected()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        var (rawKey, _) = await EnrollAsync(userManager, user);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System);
        var validCode = ComputeTotp(rawKey, DateTimeOffset.UtcNow);

        // Not a token at all.
        Assert.IsType<UnauthorizedObjectResult>(
            (await controller.MfaVerify(new MfaVerifyRequest { ChallengeToken = "not-a-jwt", Code = validCode })).Result);

        // A well-formed challenge minted 10 minutes ago — past its 5-minute TTL.
        var expired = NewChallengeService(new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-10))).Create(user.Id);
        Assert.IsType<UnauthorizedObjectResult>(
            (await controller.MfaVerify(new MfaVerifyRequest { ChallengeToken = expired.Token, Code = validCode })).Result);
    }

    [Fact]
    public async Task MfaVerify_WithRecoveryCode_Works_OnlyOnce()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        var (_, recoveryCodes) = await EnrollAsync(userManager, user);
        var recoveryCode = recoveryCodes[0];

        var mfaService = NewChallengeService(TimeProvider.System);

        // First redemption succeeds and issues tokens.
        var first = NewController(userManager, mfaService, TimeProvider.System);
        var firstResult = await first.MfaVerify(new MfaVerifyRequest
        {
            ChallengeToken = mfaService.Create(user.Id).Token,
            Code = recoveryCode
        });
        Assert.IsType<AuthResponse>(Assert.IsType<OkObjectResult>(firstResult.Result).Value);

        // The same recovery code is now consumed and can never be reused.
        var second = NewController(userManager, mfaService, TimeProvider.System);
        var secondResult = await second.MfaVerify(new MfaVerifyRequest
        {
            ChallengeToken = mfaService.Create(user.Id).Token,
            Code = recoveryCode
        });
        Assert.IsType<UnauthorizedObjectResult>(secondResult.Result);
    }

    // ----- Disable -------------------------------------------------------------------------------------

    [Fact]
    public async Task Disable_WithValidCode_TurnsOffTwoFactor_AndRotatesKey()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        var (rawKey, _) = await EnrollAsync(userManager, user);

        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System, user);
        var result = await controller.MfaDisable(
            new MfaDisableRequest { Code = ComputeTotp(rawKey, DateTimeOffset.UtcNow) });

        Assert.IsType<NoContentResult>(result);
        Assert.False(await userManager.GetTwoFactorEnabledAsync(user));
        // Key was rotated, so the previously-enrolled secret is gone.
        Assert.NotEqual(rawKey, await userManager.GetAuthenticatorKeyAsync(user));
    }

    // ----- Abuse guards --------------------------------------------------------------------------------

    [Fact]
    public async Task MfaVerify_FailedCodes_IncrementLockout_UntilLocked()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager);
        var (rawKey, _) = await EnrollAsync(userManager, user);

        var validCode = ComputeTotp(rawKey, DateTimeOffset.UtcNow);
        var wrongCode = validCode == "000000" ? "111111" : "000000";
        var challenge = NewChallengeService(TimeProvider.System).Create(user.Id);
        var controller = NewController(userManager, NewChallengeService(TimeProvider.System), TimeProvider.System);

        Assert.True(await userManager.GetLockoutEnabledAsync(user));

        // Default Identity policy locks the account after 5 consecutive failures.
        for (var i = 0; i < 5; i++)
        {
            var result = await controller.MfaVerify(
                new MfaVerifyRequest { ChallengeToken = challenge.Token, Code = wrongCode });
            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        var reloaded = await userManager.FindByIdAsync(user.Id.ToString(CultureInfo.InvariantCulture));
        Assert.True(await userManager.IsLockedOutAsync(reloaded!));
    }

    // ----- Challenge-token security property -----------------------------------------------------------

    [Fact]
    public void ChallengeToken_RoundTrips_ThroughValidate()
    {
        var service = NewChallengeService(TimeProvider.System);
        var challenge = service.Create(4242);

        Assert.Equal(4242, service.Validate(challenge.Token));
    }

    [Fact]
    public void ChallengeToken_IsRejected_ByAccessTokenValidationParameters()
    {
        var challenge = NewChallengeService(TimeProvider.System).Create(123);

        // The exact validation parameters Program.cs configures for the JwtBearer access-token pipeline.
        var accessTokenValidation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtOptionsValue.Issuer,
            ValidateAudience = true,
            ValidAudience = JwtOptionsValue.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtOptionsValue.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // The signature is valid (same key) but the audience is "MyStore:mfa-challenge", not "MyStore",
        // so the bearer pipeline rejects it out of hand — it can never stand in for an access token.
        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(challenge.Token, accessTokenValidation, out _));
    }

    // ----- Local RFC-6238 TOTP (test-only) -------------------------------------------------------------

    private static string ComputeTotp(string base32Secret, DateTimeOffset time)
    {
        var key = Base32Decode(base32Secret);
        var counter = time.ToUnixTimeSeconds() / 30;

        var counterBytes = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
                     | ((hash[offset + 1] & 0xff) << 16)
                     | ((hash[offset + 2] & 0xff) << 8)
                     | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", string.Empty);

        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input)
        {
            var index = alphabet.IndexOf(c);
            if (index < 0)
            {
                continue;
            }

            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return output.ToArray();
    }

    // ----- Test doubles for AuthController's unrelated collaborators -----------------------------------

    private sealed class FakeAntiforgery : IAntiforgery
    {
        private static AntiforgeryTokenSet Tokens() =>
            new("request-token", "cookie-token", "__RequestVerificationToken", AuthCookies.XsrfHeader);

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private sealed class FakePasswordResetService : IPasswordResetService
    {
        public Task RequestResetAsync(string email, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IdentityResult> ResetPasswordAsync(
            string email, string token, string newPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(IdentityResult.Success);
    }

    private sealed class FakeWelcomeEmailService : IWelcomeEmailService
    {
        public Task SendWelcomeEmailAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
