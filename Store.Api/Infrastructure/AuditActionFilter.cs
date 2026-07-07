using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Store.Application.Auditing;
using Store.Data.Auditing;

namespace Store.Api.Infrastructure;

/// <summary>
/// Global filter that turns a successful admin mutation into one <see cref="AuditLog"/> row. It
/// fires only for <c>/api/admin/*</c> POST/PUT/PATCH/DELETE requests that return 2xx and actually
/// changed data, reading the actor from the JWT, the area from the controller's authorization
/// policy, and the before/after values from the DbContext's captured changes. Auditing never breaks
/// the request — any failure here is swallowed.
/// </summary>
public sealed class AuditActionFilter : IAsyncActionFilter
{
    private const string AdminPathPrefix = "/api/admin";
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string PolicyAreaPrefix = "area:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private readonly IAuditContext _auditContext;
    private readonly IAuditService _auditService;

    public AuditActionFilter(IAuditContext auditContext, IAuditService auditService)
    {
        _auditContext = auditContext;
        _auditService = auditService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        try
        {
            await MaybeAuditAsync(context, executed);
        }
        catch
        {
            // Auditing is best-effort; a logging failure must not surface to the caller.
        }
    }

    private async Task MaybeAuditAsync(ActionExecutingContext context, ActionExecutedContext executed)
    {
        var request = context.HttpContext.Request;

        if (!request.Path.StartsWithSegments(AdminPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var action = ActionForMethod(request.Method);
        if (action is null)
        {
            return;
        }

        if (executed.Exception is { } || !IsSuccess(executed))
        {
            return;
        }

        // Pick the request's primary changed entity (the one touching the most fields).
        var primary = _auditContext.Changes
            .OrderByDescending(c => c.ChangedCount)
            .FirstOrDefault();

        if (primary is null)
        {
            return;
        }

        var user = context.HttpContext.User;

        var entry = new AuditEntry
        {
            UserId = TryGetUserId(user),
            UserName = user.FindFirstValue(ClaimTypes.Name)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? "unknown",
            Role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            Action = primary.State == "Deleted" ? "Delete" : action,
            EntityType = primary.EntityType,
            EntityId = primary.EntityId,
            EntityName = primary.EntityName,
            OldValuesJson = primary.OldValues.Count > 0 ? JsonSerializer.Serialize(primary.OldValues, JsonOptions) : null,
            NewValuesJson = primary.NewValues.Count > 0 ? JsonSerializer.Serialize(primary.NewValues, JsonOptions) : null,
            Area = AreaFromPolicy(context),
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = request.Headers.TryGetValue(CorrelationHeader, out var value)
                ? value.ToString()
                : null,
        };

        await _auditService.LogAsync(entry, context.HttpContext.RequestAborted);
    }

    private static string? ActionForMethod(string method) => method.ToUpperInvariant() switch
    {
        "POST" => "Create",
        "PUT" => "Update",
        "PATCH" => "Update",
        "DELETE" => "Delete",
        _ => null,
    };

    private static bool IsSuccess(ActionExecutedContext executed)
    {
        if (executed.Result is IStatusCodeActionResult { StatusCode: { } status })
        {
            return status is >= 200 and < 300;
        }

        // No explicit status (e.g. a plain ObjectResult) defaults to 200 OK.
        return true;
    }

    private static long? TryGetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static string AreaFromPolicy(ActionExecutingContext context)
    {
        var policy = context.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .LastOrDefault(p => !string.IsNullOrEmpty(p) && p!.StartsWith(PolicyAreaPrefix, StringComparison.OrdinalIgnoreCase));

        if (policy is null)
        {
            return "Admin";
        }

        var name = policy[PolicyAreaPrefix.Length..];
        return name.Length == 0
            ? "Admin"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
