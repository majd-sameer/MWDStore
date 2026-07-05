using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Grants the built-in <c>admin</c> role every permission in <see cref="Permissions.All"/> as role claims,
/// so the existing admin retains full access after the ACL rollout. Idempotent and additive — it only adds
/// permissions the role is missing, so it is safe to run on every startup. Must run after
/// <see cref="IdentitySeeder"/> (which ensures the admin role exists).
/// </summary>
public static class AclSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AclSeeder");

        var roleManager = sp.GetRequiredService<RoleManager<Role>>();

        var adminRole = await roleManager.FindByNameAsync(AppRoles.Admin);
        if (adminRole == null)
        {
            logger.LogWarning(
                "The '{Role}' role does not exist — skipping ACL seeding (IdentitySeeder must run first).",
                AppRoles.Admin);
            return;
        }

        var existing = await roleManager.GetClaimsAsync(adminRole);
        var already = existing
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in Permissions.All)
        {
            if (already.Contains(permission))
            {
                continue;
            }

            var result = await roleManager.AddClaimAsync(adminRole, new Claim(Permissions.ClaimType, permission));
            if (!result.Succeeded)
            {
                logger.LogError("Failed to grant permission '{Permission}' to the '{Role}' role: {Errors}",
                    permission, AppRoles.Admin, string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Granted permission '{Permission}' to the '{Role}' role.",
                permission, AppRoles.Admin);
        }
    }
}
