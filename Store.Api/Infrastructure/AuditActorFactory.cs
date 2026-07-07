using System.Security.Claims;
using Store.Application.Auditing;

namespace Store.Api.Infrastructure;

/// <summary>
/// Builds an <see cref="AuditActor"/> snapshot from the current request — the JWT identity plus IP and
/// correlation id — so application services can log a fully-attributed audit row without depending on
/// ASP.NET Core.
/// </summary>
public static class AuditActorFactory
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static AuditActor FromContext(HttpContext context)
    {
        var user = context.User;
        var userId = long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (long?)null;

        return new AuditActor(
            userId,
            user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "unknown",
            user.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.TryGetValue(CorrelationHeader, out var cid) ? cid.ToString() : null);
    }
}
