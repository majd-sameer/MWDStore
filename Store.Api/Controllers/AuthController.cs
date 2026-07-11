using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auth;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>
/// Registration and login, issuing short-lived JWT access tokens alongside a rotating refresh token.
/// The access token is returned in the body (the SPA keeps it in memory only); the refresh token is
/// set as an httpOnly, Secure, SameSite=Strict cookie that JavaScript can never read. A JS-readable
/// XSRF token cookie is issued on the same responses so the SPA can echo it on mutating requests.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    // The issuer/account label used to build the otpauth:// provisioning URI (what the authenticator app shows).
    private const string MfaIssuer = "MyStore";
    private const int RecoveryCodeCount = 10;

    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IWelcomeEmailService _welcomeEmailService;
    private readonly IMfaChallengeService _mfaChallengeService;
    private readonly IAntiforgery _antiforgery;
    private readonly TimeProvider _timeProvider;

    public AuthController(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IPasswordResetService passwordResetService,
        IWelcomeEmailService welcomeEmailService,
        IMfaChallengeService mfaChallengeService,
        IAntiforgery antiforgery,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _passwordResetService = passwordResetService;
        _welcomeEmailService = welcomeEmailService;
        _mfaChallengeService = mfaChallengeService;
        _antiforgery = antiforgery;
        _timeProvider = timeProvider;
    }

    [HttpPost("register")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Conflict(new { error = "An account with this email already exists." });
        }

        var now = _timeProvider.GetUtcNow();
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Email : request.FullName,
            UserGuid = Guid.NewGuid(),
            CreatedOn = now,
            LatestUpdatedOn = now
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        // Best-effort: SendWelcomeEmailAsync never throws, so a broken mail queue can't fail registration.
        await _welcomeEmailService.SendWelcomeEmailAsync(user);

        return await IssueTokenAsync(user);
    }

    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? await _userManager.FindByNameAsync(request.Email);

        if (user == null || user.IsDeleted || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // A locked-out account can neither obtain tokens nor start an MFA challenge.
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // Second factor enrolled: withhold tokens and hand back a short-lived challenge the client redeems at
        // /api/auth/mfa/verify with a valid authenticator (or recovery) code.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            var challenge = _mfaChallengeService.Create(user.Id);
            return Ok(new MfaChallengeResponse
            {
                MfaRequired = true,
                ChallengeToken = challenge.Token,
                ExpiresAt = challenge.ExpiresAt
            });
        }

        return await IssueTokenAsync(user);
    }

    /// <summary>
    /// Redeems the login challenge from <see cref="Login"/> together with a TOTP or recovery code, issuing the
    /// normal access + refresh tokens on success — the exact same <see cref="AuthResponse"/> a password login
    /// returns. Anonymous (the challenge token is the credential); failed codes count toward Identity lockout.
    /// </summary>
    [HttpPost("mfa/verify")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<AuthResponse>> MfaVerify(MfaVerifyRequest request)
    {
        var userId = _mfaChallengeService.Validate(request.ChallengeToken);
        if (userId is null)
        {
            return Unauthorized(new { error = "Invalid or expired challenge." });
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString(CultureInfo.InvariantCulture));
        if (user is null || user.IsDeleted || !await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Unauthorized(new { error = "Invalid or expired challenge." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { error = "Account temporarily locked. Try again later." });
        }

        if (!await VerifyTotpOrRecoveryCodeAsync(user, request.Code))
        {
            // Brute-force defence: each miss increments the failure count and eventually locks the account.
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { error = "Invalid authenticator or recovery code." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokenAsync(user);
    }

    /// <summary>
    /// Exchanges the httpOnly refresh cookie for a new access token, rotating the refresh token in the
    /// process (the presented token is invalidated and replaced). Authenticated purely by the cookie —
    /// no bearer required — which is why this endpoint relies on the cookie's SameSite=Strict attribute
    /// (not antiforgery) for CSRF protection, so it keeps working on a cold page load and cross-origin.
    /// </summary>
    [HttpPost("refresh")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        if (!Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) || string.IsNullOrEmpty(raw))
        {
            return Unauthorized(new { error = "No refresh token." });
        }

        var hash = _refreshTokenService.Hash(raw);
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshTokenHash == hash && !u.IsDeleted);

        if (user is null
            || user.RefreshTokenExpiresAt is null
            || user.RefreshTokenExpiresAt <= _timeProvider.GetUtcNow())
        {
            AuthCookies.ClearRefreshToken(Response);
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        return await IssueTokenAsync(user);
    }

    /// <summary>Revokes the current refresh token server-side and clears the auth cookies.</summary>
    [HttpPost("logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) && !string.IsNullOrEmpty(raw))
        {
            var hash = _refreshTokenService.Hash(raw);
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash);
            if (user is not null)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiresAt = null;
                await _userManager.UpdateAsync(user);
            }
        }

        AuthCookies.ClearRefreshToken(Response);
        return NoContent();
    }

    /// <summary>
    /// Always returns 200 regardless of whether an account exists for the given email, so the response
    /// can never be used to enumerate registered accounts. If the account exists, enqueues a
    /// <c>Customer.PasswordReset</c> email with a storefront reset link (best-effort — enqueue failures
    /// are logged, not thrown).
    /// </summary>
    [HttpPost("forgot-password")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _passwordResetService.RequestResetAsync(request.Email);
        return Ok();
    }

    /// <summary>
    /// Consumes an Identity password-reset token to set a new password. On success, also revokes the
    /// user's refresh token so existing sessions cannot outlive the password change.
    /// </summary>
    [HttpPost("reset-password")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _passwordResetService.ResetPasswordAsync(
            request.Email, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok();
    }

    /// <summary>
    /// Issues (or refreshes) the XSRF token cookie without requiring authentication, so the SPA can
    /// obtain a token on first load before any mutating request. Returns 204.
    /// </summary>
    [HttpGet("xsrf")]
    [IgnoreAntiforgeryToken]
    public IActionResult Xsrf()
    {
        IssueXsrfCookie();
        return NoContent();
    }

    // ----- Account-level MFA management ----------------------------------------------------------------
    // These manage the signed-in user's OWN second factor, so they are [Authorize] (any authenticated user),
    // not admin-only. They live on the /api/account/mfa/* path (rooted with "~/" to escape this controller's
    // /api/auth prefix) while staying colocated with the rest of the auth/MFA logic.

    /// <summary>
    /// Begins (or restarts) enrolment: resets the authenticator key and returns the fresh shared secret plus its
    /// <c>otpauth://</c> URI. Resetting on each call invalidates any half-finished prior enrolment. The secret is
    /// only usable once <see cref="MfaEnable"/> confirms a valid code, so a reset never weakens an active setup
    /// beyond what the user themselves triggers.
    /// </summary>
    [HttpGet("~/api/account/mfa/setup")]
    [HttpPost("~/api/account/mfa/setup")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<MfaSetupResponse>> MfaSetup()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            // Should never happen right after a reset, but never return a half-built setup.
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Could not generate an authenticator key." });
        }

        return Ok(new MfaSetupResponse
        {
            SharedKey = FormatKey(key),
            AuthenticatorUri = BuildAuthenticatorUri(user.Email ?? user.UserName ?? MfaIssuer, key)
        });
    }

    /// <summary>Reports whether the signed-in user currently has a second factor enrolled.</summary>
    [HttpGet("~/api/account/mfa/status")]
    [Authorize]
    public async Task<ActionResult<MfaStatusResponse>> MfaStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new MfaStatusResponse { Enabled = user.TwoFactorEnabled });
    }

    /// <summary>
    /// Confirms enrolment: verifies a current code against the pending authenticator key, turns on
    /// <c>TwoFactorEnabled</c>, and returns 10 one-time recovery codes (shown to the user exactly once).
    /// </summary>
    [HttpPost("~/api/account/mfa/enable")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<MfaEnableResponse>> MfaEnable(MfaEnableRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return BadRequest(new { error = "Two-factor authentication is already enabled." });
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(request.Code));
        if (!isValid)
        {
            return BadRequest(new { error = "Invalid authenticator code." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        return Ok(new MfaEnableResponse { RecoveryCodes = recoveryCodes?.ToArray() ?? [] });
    }

    /// <summary>
    /// Turns MFA off after proving control of the second factor (a current authenticator code or an unused
    /// recovery code), then rotates the authenticator key so the previously-enrolled device is useless if MFA
    /// is ever re-enabled. Identity exposes no public "delete key" API; rotating to a fresh, never-returned key
    /// is the equivalent — the old secret can no longer produce accepted codes.
    /// </summary>
    [HttpPost("~/api/account/mfa/disable")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MfaDisable(MfaDisableRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return BadRequest(new { error = "Two-factor authentication is not enabled." });
        }

        if (!await VerifyTotpOrRecoveryCodeAsync(user, request.Code))
        {
            return BadRequest(new { error = "Invalid authenticator or recovery code." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        return NoContent();
    }

    // Accepts either a current authenticator TOTP or an unused one-time recovery code. Redeeming a recovery
    // code consumes it (so it can never be reused).
    private async Task<bool> VerifyTotpOrRecoveryCodeAsync(User user, string code)
    {
        if (await _userManager.VerifyTwoFactorTokenAsync(
                user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(code)))
        {
            return true;
        }

        var redemption = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, code?.Trim() ?? string.Empty);
        return redemption.Succeeded;
    }

    // Authenticator apps often display the 6-digit code with a space in the middle; strip whitespace/hyphens.
    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

    // Groups the base32 secret into blocks of four (lower-cased) for readable manual entry.
    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0)
            {
                result.Append(' ');
            }

            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string BuildAuthenticatorUri(string account, string key) => string.Format(
        CultureInfo.InvariantCulture,
        "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6&period=30&algorithm=SHA1",
        Uri.EscapeDataString(MfaIssuer),
        Uri.EscapeDataString(account),
        key);

    private async Task<ActionResult<AuthResponse>> IssueTokenAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user.Id, user.UserName, user.Email, roles);

        // Rotate the refresh token: store only its hash, hand the raw value to the client via cookie.
        var refresh = _refreshTokenService.Issue();
        user.RefreshTokenHash = refresh.Hash;
        user.RefreshTokenExpiresAt = refresh.ExpiresAt;
        user.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _userManager.UpdateAsync(user);

        AuthCookies.SetRefreshToken(Response, refresh.RawToken, refresh.ExpiresAt);
        IssueXsrfCookie();

        return Ok(new AuthResponse
        {
            AccessToken = token.Token,
            ExpiresAt = token.ExpiresAt,
            UserId = user.Id,
            Email = user.Email!,
            FullName = user.FullName
        });
    }

    // Generates a fresh antiforgery token pair: the cookie-token (httpOnly, set by the framework) plus
    // the request-token, which we expose in the JS-readable XSRF-TOKEN cookie for the SPA to echo back.
    private void IssueXsrfCookie()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            AuthCookies.SetXsrf(Response, tokens.RequestToken);
        }
    }
}
