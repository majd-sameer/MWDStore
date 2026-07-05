using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Store.Data;

namespace Store.Api.Infrastructure;

/// <summary>
/// Marks a controller/action as requiring a specific <see cref="Permissions"/> value. Implemented as an
/// <see cref="AuthorizeAttribute"/> whose policy name carries the permission behind <see cref="PolicyPrefix"/>;
/// <see cref="PermissionPolicyProvider"/> turns that name into a real policy (authenticated user +
/// <see cref="PermissionRequirement"/>). If the policy provider is NOT registered, the default provider returns
/// no policy for the prefixed name and the authorization middleware throws at request time — the attribute
/// fails closed (deny/500), it never silently allows the request through.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERM:";

    public RequirePermissionAttribute(string permission)
        : base(PolicyPrefix + permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>The authorization requirement carrying the permission that must be granted.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// Builds authorization policies on demand for <see cref="RequirePermissionAttribute.PolicyPrefix"/>-prefixed
/// policy names, delegating everything else (e.g. the default/role policies) to the framework default provider.
/// Each generated policy requires an authenticated user plus the matching <see cref="PermissionRequirement"/>.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>
/// Default-deny authorization handler for <see cref="PermissionRequirement"/>. It only ever calls
/// <see cref="AuthorizationHandlerContext.Succeed"/> when the requested permission is (a) a known permission,
/// (b) held by an authenticated user, (c) whose roles actually carry the permission role claim in the database.
/// Every other path (unknown permission, unauthenticated, no roles, permission not granted, resolver failure)
/// leaves the requirement unmet, which the middleware treats as a denial.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRolePermissionReader _reader;

    public PermissionAuthorizationHandler(IRolePermissionReader reader) => _reader = reader;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Fail closed: an unknown/typo'd permission can never be satisfied.
        if (!Permissions.IsDefined(requirement.Permission))
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        if (roles.Length == 0)
        {
            return;
        }

        var granted = await _reader.GetPermissionsAsync(roles, CancellationToken.None);
        if (granted.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Resolves the set of permissions granted to a set of role names via their role claims.</summary>
public interface IRolePermissionReader
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken);
}

/// <summary>
/// Per-request DB resolution of role permissions (registered scoped). Permissions are read live from the
/// role claims table on every request so that revoking a permission or a role takes effect immediately, with
/// no dependency on token contents or a re-login. Results are memoized for the lifetime of the request (one
/// scope) to avoid re-querying when several handlers run for the same principal.
/// </summary>
public sealed class RolePermissionReader : IRolePermissionReader
{
    private readonly StoreDbContext _db;
    private readonly Dictionary<string, IReadOnlySet<string>> _cache = new(StringComparer.Ordinal);

    public RolePermissionReader(StoreDbContext db) => _db = db;

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(
        IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        // Identity stores role names normalized to upper-invariant; match on that.
        var normalized = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            return EmptySet;
        }

        var cacheKey = string.Join('\n', normalized);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var values = await (
                from role in _db.Roles
                where role.NormalizedName != null && normalized.Contains(role.NormalizedName)
                join claim in _db.RoleClaims on role.Id equals claim.RoleId
                where claim.ClaimType == Permissions.ClaimType && claim.ClaimValue != null
                select claim.ClaimValue)
            .Distinct()
            .ToListAsync(cancellationToken);

        IReadOnlySet<string> result = values.ToHashSet(StringComparer.Ordinal);
        _cache[cacheKey] = result;
        return result;
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>(StringComparer.Ordinal);
}
