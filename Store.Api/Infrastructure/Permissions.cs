namespace Store.Api.Infrastructure;

/// <summary>
/// The complete catalog of granular admin permissions. Each constant is stored as a role claim
/// (<see cref="ClaimType"/>) on the ASP.NET Identity <c>Role</c>; a user holds a permission when any of
/// their roles carries the matching role claim. This is the single source of truth — the seeder grants the
/// admin role <see cref="All"/>, the management API validates against <see cref="IsDefined"/>, and the
/// authorization handler fails closed on anything not in this set.
/// </summary>
public static class Permissions
{
    /// <summary>The claim type under which a permission value is stored on a role (and never in the JWT —
    /// permissions are resolved per request from the database so revocation takes effect without re-login).</summary>
    public const string ClaimType = "permission";

    // Catalog — products, categories, brands, attributes, options, templates, reviews, inventory, vendors.
    public const string CatalogView = "Catalog.View";
    public const string CatalogManage = "Catalog.Manage";

    // Orders — browsing/processing orders and shipments, plus the higher-trust refund/finance capability.
    public const string OrdersView = "Orders.View";
    public const string OrdersManage = "Orders.Manage";
    public const string OrdersRefund = "Orders.Refund";

    // Customers — customer accounts and customer groups.
    public const string CustomersView = "Customers.View";
    public const string CustomersManage = "Customers.Manage";

    // Content — CMS pages, news, menus, comments, contact submissions.
    public const string ContentManage = "Content.Manage";

    // Settings — store settings, shipping, tax, locations, localization, system logs.
    public const string SettingsManage = "Settings.Manage";

    // Media — the shared media library.
    public const string MediaManage = "Media.Manage";

    // Reports — dashboard and read-only reporting surfaces.
    public const string ReportsView = "Reports.View";

    // ACL — managing roles, role permissions, and user-role assignments (this feature's own admin surface).
    public const string AclManage = "Acl.Manage";

    /// <summary>Every defined permission. The admin role is seeded with all of these.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        CatalogView,
        CatalogManage,
        OrdersView,
        OrdersManage,
        OrdersRefund,
        CustomersView,
        CustomersManage,
        ContentManage,
        SettingsManage,
        MediaManage,
        ReportsView,
        AclManage
    ];

    private static readonly HashSet<string> Defined = new(All, StringComparer.Ordinal);

    /// <summary>True when <paramref name="permission"/> is a known permission. Used to fail closed on
    /// unknown/typo'd permission names both at enforcement time and when saving a role's permissions.</summary>
    public static bool IsDefined(string? permission) =>
        !string.IsNullOrEmpty(permission) && Defined.Contains(permission);
}
