using Microsoft.AspNetCore.Identity;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Ensures the <c>admin</c> role exists and a bootstrap admin user is present and in that role. Idempotent —
/// safe to run on every startup. Skips user creation when no admin password is configured.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        var roleManager = sp.GetRequiredService<RoleManager<Role>>();
        var userManager = sp.GetRequiredService<UserManager<User>>();
        var options = sp.GetRequiredService<AdminSeedOptions>();
        var timeProvider = sp.GetRequiredService<TimeProvider>();

        // 1) Ensure every well-known role (the six staff roles + customer) exists.
        foreach (var roleName in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new Role { Name = roleName });
            if (!roleResult.Succeeded)
            {
                logger.LogError("Failed to create the '{Role}' role: {Errors}",
                    roleName, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created the '{Role}' role.", roleName);
        }

        // 1c) Ensure the shared guest account that owns no-login orders. It has no role and a
        // throwaway random password, so it can never be signed into; guest checkout only needs its id.
        var guest = await userManager.FindByEmailAsync(GuestUser.Email);
        if (guest == null)
        {
            var now = timeProvider.GetUtcNow();
            guest = new User
            {
                UserName = GuestUser.Email,
                Email = GuestUser.Email,
                FullName = GuestUser.FullName,
                UserGuid = Guid.NewGuid(),
                CreatedOn = now,
                LatestUpdatedOn = now
            };

            // A long random password that is never persisted anywhere — the account is not meant to log in.
            var guestResult = await userManager.CreateAsync(guest, $"Guest!{Guid.NewGuid():N}A1");
            if (!guestResult.Succeeded)
            {
                logger.LogError("Failed to create the guest account: {Errors}",
                    string.Join("; ", guestResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created the guest account '{Email}'.", GuestUser.Email);
        }

        // 2) Ensure the admin user (only when a password is configured).
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("No admin password configured ('{Section}:Password') — skipping admin user seeding.",
                AdminSeedOptions.SectionName);
            return;
        }

        var user = await userManager.FindByEmailAsync(options.Email);
        if (user == null)
        {
            var now = timeProvider.GetUtcNow();
            user = new User
            {
                UserName = options.Email,
                Email = options.Email,
                FullName = options.FullName,
                UserGuid = Guid.NewGuid(),
                CreatedOn = now,
                LatestUpdatedOn = now
            };

            var createResult = await userManager.CreateAsync(user, options.Password);
            if (!createResult.Succeeded)
            {
                logger.LogError("Failed to create the admin user: {Errors}",
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created the admin user '{Email}'.", options.Email);
        }

        // 3) Ensure the bootstrap user is a super admin — the only role that can manage users/roles,
        //    so without it nobody could ever assign the other roles. Additive: an existing admin-only
        //    account is upgraded on the next startup and keeps any roles it already had.
        if (!await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
        {
            var addResult = await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);
            if (!addResult.Succeeded)
            {
                logger.LogError("Failed to add '{Email}' to the '{Role}' role: {Errors}",
                    options.Email, AppRoles.SuperAdmin, string.Join("; ", addResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Granted '{Email}' the '{Role}' role.", options.Email, AppRoles.SuperAdmin);
        }
    }
}
