using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.Auth;
using Store.Data;
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
    /// <summary>
    /// Ceiling on concurrent signed-in sessions per user; issuing beyond it evicts the oldest.
    /// Generous enough that no real shopper hits it, small enough that a credential-stuffing loop
    /// cannot grow the token table unboundedly for one account.
    /// </summary>
    private const int MaxSessionsPerUser = 20;

    private readonly UserManager<User> _userManager;
    private readonly StoreDbContext _db;
    private readonly IJwtTokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAntiforgery _antiforgery;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditService _auditService;

    public AuthController(
        UserManager<User> userManager,
        StoreDbContext db,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IAntiforgery antiforgery,
        TimeProvider timeProvider,
        IAuditService auditService)
    {
        _userManager = userManager;
        _db = db;
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
    /// Exchanges the httpOnly refresh cookie for a new access token, rotating the presented refresh
    /// token in the process (that token is invalidated and replaced; the user's other sessions keep
    /// theirs). Authenticated purely by the cookie — no bearer required — which is why this endpoint
    /// relies on the cookie's SameSite=Strict attribute (not antiforgery) for CSRF protection, so it
    /// keeps working on a cold page load and cross-origin.
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
        var token = await _db.UserRefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token is null || token.ExpiresAt <= _timeProvider.GetUtcNow() || token.User.IsDeleted)
        {
            if (token is not null)
            {
                _db.UserRefreshTokens.Remove(token);
                await _db.SaveChangesAsync();
            }

            AuthCookies.ClearRefreshToken(Response);
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        return await IssueTokenAsync(token.User, rotating: token);
    }

    /// <summary>Revokes this session's refresh token server-side and clears the auth cookies.</summary>
    [HttpPost("logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) && !string.IsNullOrEmpty(raw))
        {
            var hash = _refreshTokenService.Hash(raw);
            var token = await _db.UserRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token is not null)
            {
                _db.UserRefreshTokens.Remove(token);
                await _db.SaveChangesAsync();
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

    private async Task<ActionResult<AuthResponse>> IssueTokenAsync(User user, UserRefreshToken? rotating = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user.Id, user.UserName, user.Email, roles);

        // A fresh refresh token per issuance: a new row on login (each browser/device holds its own
        // session), or a replacement for the presented row on refresh. Only the hash is stored.
        var refresh = _refreshTokenService.Issue();
        var now = _timeProvider.GetUtcNow();

        if (rotating is not null)
        {
            _db.UserRefreshTokens.Remove(rotating);
        }

        // Housekeeping while we're here: drop this user's expired tokens, and if they still have a
        // full house of live sessions, evict the oldest to make room for the one being issued.
        var rotatingId = rotating?.Id ?? 0;
        var existing = await _db.UserRefreshTokens
            .Where(t => t.UserId == user.Id && t.Id != rotatingId)
            .OrderByDescending(t => t.CreatedOn)
            .ToListAsync();
        foreach (var stale in existing.Where(t => t.ExpiresAt <= now)
                     .Union(existing.Where(t => t.ExpiresAt > now).Skip(MaxSessionsPerUser - 1)))
        {
            _db.UserRefreshTokens.Remove(stale);
        }

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedOn = now
        });
        await _db.SaveChangesAsync();

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
