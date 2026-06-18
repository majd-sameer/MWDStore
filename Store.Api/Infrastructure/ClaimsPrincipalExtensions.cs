using System.Security.Claims;

namespace Store.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's id. The JWT carries it as <c>sub</c>, which the bearer handler maps to
    /// <see cref="ClaimTypes.NameIdentifier"/> (the same claim <c>UserManager.GetUserId</c> reads).
    /// </summary>
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The access token does not contain a valid user id.");
    }
}
