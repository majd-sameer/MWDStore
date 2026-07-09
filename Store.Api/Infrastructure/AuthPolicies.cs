using Microsoft.AspNetCore.Authorization;

namespace Store.Api.Infrastructure;

/// <summary>
/// Named authorization policies for the admin surface. Each policy maps an area of the back-office
/// to the <see cref="AppRoles"/> that may reach it; every admin controller carries
/// <c>[Authorize(Policy = ...)]</c> with its area. <see cref="AppRoles.SuperAdmin"/> and
/// <see cref="AppRoles.Admin"/> are members of every operational policy, so they retain the full
/// access they had before roles were split out.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Products, categories, brands, options, attributes, templates.</summary>
    public const string Catalog = "area:catalog";

    /// <summary>CMS: pages, menus, news.</summary>
    public const string Content = "area:content";

    /// <summary>Reviews and comment moderation.</summary>
    public const string Moderation = "area:moderation";

    /// <summary>Media library uploads (used by catalog &amp; CMS editors).</summary>
    public const string Media = "area:media";

    /// <summary>Inventory and warehouses.</summary>
    public const string Inventory = "area:inventory";

    /// <summary>Shipping configuration and shipment processing.</summary>
    public const string Fulfillment = "area:fulfillment";

    /// <summary>Read an order's shipment records — order viewers plus fulfilment staff.</summary>
    public const string ShipmentsView = "area:shipments-view";

    /// <summary>Orders, customer directory, contacts, customer groups.</summary>
    public const string Sales = "area:sales";

    /// <summary>View orders and their detail — sales roles plus warehouse (fulfilment) staff.</summary>
    public const string OrdersView = "area:orders-view";

    /// <summary>Vendor directory (shared by sales and sales managers).</summary>
    public const string Vendors = "area:vendors";

    /// <summary>Promotions.</summary>
    public const string Marketing = "area:marketing";

    /// <summary>Tax rates and tax classes.</summary>
    public const string Taxes = "area:taxes";

    /// <summary>Payment providers and their configuration.</summary>
    public const string Payments = "area:payments";

    /// <summary>Dashboard / reporting.</summary>
    public const string Reports = "area:reports";

    /// <summary>Store settings, localization, locations, logs.</summary>
    public const string Settings = "area:settings";

    /// <summary>User &amp; role management.</summary>
    public const string Users = "area:users";

    /// <summary>Registers every admin-area policy on the authorization options.</summary>
    public static void AddStorePolicies(this AuthorizationOptions options)
    {
        void Area(string name, params string[] roles) =>
            options.AddPolicy(name, policy => policy.RequireRole(roles));

        Area(Catalog, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.WarehouseKeeper);
        Area(Content, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.ContentWriter);
        Area(Moderation, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.ContentWriter);
        Area(Media, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.ContentWriter, AppRoles.WarehouseKeeper);
        Area(Inventory, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.WarehouseKeeper);
        Area(Fulfillment, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.WarehouseKeeper);
        Area(ShipmentsView, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Sales, AppRoles.SalesManager, AppRoles.WarehouseKeeper);
        Area(Sales, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Sales, AppRoles.SalesManager);
        Area(OrdersView, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Sales, AppRoles.SalesManager, AppRoles.WarehouseKeeper);
        Area(Vendors, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Sales, AppRoles.SalesManager);
        Area(Marketing, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.SalesManager);
        Area(Taxes, AppRoles.SuperAdmin, AppRoles.Admin);
        Area(Payments, AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.SalesManager);
        Area(Reports, AppRoles.SuperAdmin, AppRoles.Admin);
        Area(Settings, AppRoles.SuperAdmin, AppRoles.Admin);
        Area(Users, AppRoles.SuperAdmin, AppRoles.Admin);
    }
}
