namespace Store.Api.Infrastructure;

/// <summary>
/// Well-known role names. The back-office (admin app) is gated by the six <see cref="Staff"/> roles,
/// each granting a slice of the admin surface (see <see cref="AuthPolicies"/>); storefront shoppers
/// hold <see cref="Customer"/>. Names are lowercase/kebab-case because that exact string travels in
/// the JWT role claim and is compared verbatim by the Angular route guards.
/// </summary>
public static class AppRoles
{
    /// <summary>Unrestricted back-office access, including user &amp; role management.</summary>
    public const string SuperAdmin = "super-admin";

    /// <summary>Full back-office access except managing other users/roles.</summary>
    public const string Admin = "admin";

    /// <summary>Sales oversight: orders, customers plus promotions, tax and the dashboard.</summary>
    public const string SalesManager = "sales-manager";

    /// <summary>Order processing and the customer directory.</summary>
    public const string Sales = "sales";

    /// <summary>Stock and fulfilment: inventory, warehouses, shipping.</summary>
    public const string WarehouseKeeper = "warehouse-keeper";

    /// <summary>Catalog content and CMS: products, categories, brands, pages, news, moderation.</summary>
    public const string ContentWriter = "content-writer";

    /// <summary>Storefront shoppers; assigned when a customer is created.</summary>
    public const string Customer = "customer";

    /// <summary>All back-office (staff) roles — everything that may enter the admin app; excludes <see cref="Customer"/>.</summary>
    public static readonly string[] Staff =
    [
        SuperAdmin, Admin, SalesManager, Sales, WarehouseKeeper, ContentWriter
    ];

    /// <summary>Every role the identity seeder ensures exists.</summary>
    public static readonly string[] All =
    [
        SuperAdmin, Admin, SalesManager, Sales, WarehouseKeeper, ContentWriter, Customer
    ];
}
