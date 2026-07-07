using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
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
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAntiforgery _antiforgery;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditService _auditService;

    public AuthController(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        TimeProvider timeProvider,
        IAuditService auditService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _antiforgery = antiforgery;
        _timeProvider = timeProvider;
        _auditService = auditService;
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

        await AuditStaffLoginAsync(user);
        return await IssueTokenAsync(user);
    }

    /// <summary>
    /// Records a back-office sign-in in the audit trail. Storefront (customer-only) logins are not
    /// audited — the trail is for staff actions. Best-effort: a logging failure never blocks login.
    /// </summary>
    private async Task AuditStaffLoginAsync(User user)
    {
        try
        {
            var roles = await _userManager.GetRolesAsync(user);
            var staffRole = roles.FirstOrDefault(r => AppRoles.Staff.Contains(r));
            if (staffRole is null)
            {
                return;
            }

            await _auditService.LogAsync(new AuditEntry
            {
                UserId = user.Id,
                UserName = user.UserName ?? user.Email ?? "unknown",
                Role = staffRole,
                Action = "Login",
                EntityType = "User",
                EntityId = user.Id,
                EntityName = user.FullName ?? user.UserName,
                Area = "Account",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CorrelationId = Request.Headers.TryGetValue("X-Correlation-Id", out var cid)
                    ? cid.ToString()
                    : null,
            });
        }
        catch
        {
            // Auditing must not break authentication.
        }
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
