using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Store.Api.Controllers.Admin;
using Store.Api.Infrastructure;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the granular-permission ACL: the default-deny authorization handler, the dynamic policy provider,
/// the admin permission seeder, and the management controller's role/permission round-trips. Everything runs
/// against EF InMemory + a real RoleManager/UserManager wired the same way Store.Api's Program.cs wires them.
/// </summary>
public class AclTests
{
    // ----- Identity manager helpers (mirrors PasswordResetServiceTests.NewUserManager) ------------------

    private static ServiceProvider BuildIdentityProvider(StoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddDataProtection();
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredUniqueChars = 0;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<StoreDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }

    private static AuthorizationHandlerContext HandlerContext(
        string permission, string[] roles, bool authenticated = true)
    {
        var requirement = new PermissionRequirement(permission);
        // A non-null authentication type is what makes ClaimsIdentity.IsAuthenticated true.
        var identity = authenticated
            ? new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth")
            : new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    // ----- Authorization handler (enforcement) ---------------------------------------------------------

    [Fact]
    public async Task Handler_Grants_WhenRoleCarriesThePermissionClaim()
    {
        using var db = TestDb.New();
        db.Roles.Add(new Role { Id = 10, Name = "catalog-editor", NormalizedName = "CATALOG-EDITOR" });
        db.RoleClaims.Add(new RoleClaim
        {
            Id = 1, RoleId = 10, ClaimType = Permissions.ClaimType, ClaimValue = Permissions.CatalogManage
        });
        await db.SaveChangesAsync();

        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));
        var context = HandlerContext(Permissions.CatalogManage, ["catalog-editor"]);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_Denies_WhenRoleLacksThePermission()
    {
        using var db = TestDb.New();
        db.Roles.Add(new Role { Id = 11, Name = "catalog-editor", NormalizedName = "CATALOG-EDITOR" });
        db.RoleClaims.Add(new RoleClaim
        {
            Id = 1, RoleId = 11, ClaimType = Permissions.ClaimType, ClaimValue = Permissions.CatalogManage
        });
        await db.SaveChangesAsync();

        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));
        // The role has Catalog.Manage but the endpoint demands Orders.Refund.
        var context = HandlerContext(Permissions.OrdersRefund, ["catalog-editor"]);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_Denies_WhenUserHasNoRoles()
    {
        using var db = TestDb.New();
        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));
        var context = HandlerContext(Permissions.CatalogManage, roles: []);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_Denies_WhenUnauthenticated()
    {
        using var db = TestDb.New();
        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));
        var context = HandlerContext(Permissions.CatalogManage, ["admin"], authenticated: false);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_Denies_UnknownPermission_EvenIfClaimMatches()
    {
        using var db = TestDb.New();
        // A role that literally carries the (bogus) permission string as a claim value.
        db.Roles.Add(new Role { Id = 12, Name = "weird", NormalizedName = "WEIRD" });
        db.RoleClaims.Add(new RoleClaim
        {
            Id = 1, RoleId = 12, ClaimType = Permissions.ClaimType, ClaimValue = "Bogus.Permission"
        });
        await db.SaveChangesAsync();

        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));
        var context = HandlerContext("Bogus.Permission", ["weird"]);

        await handler.HandleAsync(context);

        // Fail closed: an undefined permission can never be satisfied, claim or not.
        Assert.False(context.HasSucceeded);
    }

    // ----- Policy provider -----------------------------------------------------------------------------

    [Fact]
    public async Task PolicyProvider_BuildsAuthenticatedPermissionPolicy_ForPrefixedName()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(
            RequirePermissionAttribute.PolicyPrefix + Permissions.SettingsManage);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements.OfType<PermissionRequirement>(),
            r => r.Permission == Permissions.SettingsManage);
        // Anonymous requests are rejected before the permission is even considered.
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task PolicyProvider_FallsBack_ForNonPermissionPolicyNames()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("SomeUnrelatedPolicy");

        Assert.Null(policy);
    }

    // ----- Seeder --------------------------------------------------------------------------------------

    [Fact]
    public async Task AclSeeder_GrantsAdminEveryPermission_AndIsIdempotent()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        await roleManager.CreateAsync(new Role { Name = AppRoles.Admin });

        // Run twice — the second run must not create duplicate claims.
        await AclSeeder.SeedAsync(provider);
        await AclSeeder.SeedAsync(provider);

        var admin = await roleManager.FindByNameAsync(AppRoles.Admin);
        var claims = await roleManager.GetClaimsAsync(admin!);
        var permissionClaims = claims.Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).ToList();

        Assert.Equal(Permissions.All.OrderBy(p => p), permissionClaims.OrderBy(p => p));
        Assert.Equal(Permissions.All.Count, permissionClaims.Count); // no duplicates after two runs
    }

    [Fact]
    public async Task AclSeeder_SeededAdmin_PassesEveryPermissionCheck()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        await roleManager.CreateAsync(new Role { Name = AppRoles.Admin });
        await AclSeeder.SeedAsync(provider);

        var handler = new PermissionAuthorizationHandler(new RolePermissionReader(db));

        foreach (var permission in Permissions.All)
        {
            var context = HandlerContext(permission, [AppRoles.Admin]);
            await handler.HandleAsync(context);
            Assert.True(context.HasSucceeded, $"admin should hold {permission}");
        }
    }

    // ----- Management controller round-trips -----------------------------------------------------------

    private static T Body<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<T>(ok.Value);
    }

    [Fact]
    public async Task Controller_SetPermissions_RoundTrips_SetAndUnset()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        var userManager = provider.GetRequiredService<UserManager<User>>();
        await roleManager.CreateAsync(new Role { Name = "order-processor" });

        var controller = new AdminAclController(roleManager, userManager);

        // Set two permissions.
        var afterSet = Body(await controller.SetPermissions(
            "order-processor",
            new AclSetPermissionsRequest([Permissions.OrdersView, Permissions.OrdersManage]),
            default));
        Assert.Equal([Permissions.OrdersManage, Permissions.OrdersView], afterSet.Permissions);

        // Reduce to a single permission — the removed one must be gone.
        var afterUnset = Body(await controller.SetPermissions(
            "order-processor",
            new AclSetPermissionsRequest([Permissions.OrdersView]),
            default));
        Assert.Equal([Permissions.OrdersView], afterUnset.Permissions);
    }

    [Fact]
    public async Task Controller_SetPermissions_RejectsUnknownPermission()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        var userManager = provider.GetRequiredService<UserManager<User>>();
        await roleManager.CreateAsync(new Role { Name = "finance" });

        var controller = new AdminAclController(roleManager, userManager);

        var result = await controller.SetPermissions(
            "finance", new AclSetPermissionsRequest(["Not.A.Real.Permission"]), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);

        // Nothing was persisted.
        var role = await roleManager.FindByNameAsync("finance");
        var claims = await roleManager.GetClaimsAsync(role!);
        Assert.DoesNotContain(claims, c => c.Type == Permissions.ClaimType);
    }

    [Fact]
    public async Task Controller_AssignAndRemoveUserRole_TogglesMembership()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        var userManager = provider.GetRequiredService<UserManager<User>>();
        await roleManager.CreateAsync(new Role { Name = "catalog-editor" });

        var user = new User
        {
            UserName = "editor@example.com",
            Email = "editor@example.com",
            FullName = "Ed Itor",
            UserGuid = Guid.NewGuid(),
            CreatedOn = DateTimeOffset.UtcNow,
            LatestUpdatedOn = DateTimeOffset.UtcNow
        };
        Assert.True((await userManager.CreateAsync(user, "Pass@1234")).Succeeded);

        var controller = new AdminAclController(roleManager, userManager);

        Assert.IsType<NoContentResult>(
            await controller.AssignRole(user.Id, new AclAssignRoleRequest("catalog-editor"), default));
        Assert.Contains("catalog-editor", await userManager.GetRolesAsync(user));

        Assert.IsType<NoContentResult>(
            await controller.RemoveRole(user.Id, "catalog-editor", default));
        Assert.DoesNotContain("catalog-editor", await userManager.GetRolesAsync(user));
    }

    [Fact]
    public async Task Controller_AssignUnknownRole_IsRejected()
    {
        using var db = TestDb.New();
        await using var provider = BuildIdentityProvider(db);
        var roleManager = provider.GetRequiredService<RoleManager<Role>>();
        var userManager = provider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = "u@example.com", Email = "u@example.com", FullName = "U", UserGuid = Guid.NewGuid(),
            CreatedOn = DateTimeOffset.UtcNow, LatestUpdatedOn = DateTimeOffset.UtcNow
        };
        Assert.True((await userManager.CreateAsync(user, "Pass@1234")).Succeeded);

        var controller = new AdminAclController(roleManager, userManager);

        var result = await controller.AssignRole(user.Id, new AclAssignRoleRequest("ghost-role"), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Controller_GetPermissions_ReturnsFullCatalog()
    {
        using var db = TestDb.New();
        using var provider = BuildIdentityProvider(db);
        var controller = new AdminAclController(
            provider.GetRequiredService<RoleManager<Role>>(),
            provider.GetRequiredService<UserManager<User>>());

        var ok = Assert.IsType<OkObjectResult>(controller.GetPermissions().Result);
        var permissions = Assert.IsAssignableFrom<IReadOnlyList<string>>(ok.Value);
        Assert.Equal(Permissions.All, permissions);
    }
}
