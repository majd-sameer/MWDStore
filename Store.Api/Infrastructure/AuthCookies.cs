namespace Store.Api.Infrastructure;

/// <summary>
/// Centralises the names and attributes of the two auth-related cookies the API sets:
/// the httpOnly refresh-token cookie (never visible to JS) and the JS-readable XSRF token cookie
/// the SPA echoes back in the <c>X-XSRF-TOKEN</c> header.
/// </summary>
public static class AuthCookies
{
    /// <summary>httpOnly cookie carrying the rotating refresh token. Scoped to the auth endpoints.</summary>
    public const string RefreshToken = "refresh_token";

    /// <summary>JS-readable cookie name Angular's <c>withXsrfConfiguration</c> reads the token from.</summary>
    public const string Xsrf = "XSRF-TOKEN";

    /// <summary>Header Angular sends the XSRF token in (must match the antiforgery <c>HeaderName</c>).</summary>
    public const string XsrfHeader = "X-XSRF-TOKEN";

    // The refresh cookie is only ever needed by /api/auth/refresh and /api/auth/logout, so we scope
    // it there rather than sending it on every API request.
    private const string RefreshPath = "/api/auth";

    /// <summary>
    /// Writes the refresh token as an httpOnly, Secure, SameSite=Strict cookie. SameSite=Strict means
    /// the browser never attaches it to cross-site requests, which is the primary CSRF defence for the
    /// refresh/logout endpoints (a malicious site cannot trigger a rotation/logout on the user's behalf).
    /// </summary>
    public static void SetRefreshToken(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(RefreshToken, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshPath,
            Expires = expiresAt,
            IsEssential = true,
        });

    /// <summary>Expires the refresh cookie. The attributes must match <see cref="SetRefreshToken"/> to delete it.</summary>
    public static void ClearRefreshToken(HttpResponse response) =>
        response.Cookies.Delete(RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshPath,
        });

    /// <summary>
    /// Writes the antiforgery request token to a JS-readable cookie at the site root so the SPA can
    /// echo it on mutating same-origin requests. Not httpOnly (by design — the client must read it).
    /// </summary>
    public static void SetXsrf(HttpResponse response, string token) =>
        response.Cookies.Append(Xsrf, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
}
