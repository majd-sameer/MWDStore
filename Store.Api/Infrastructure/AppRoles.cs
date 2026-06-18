namespace Store.Api.Infrastructure;

/// <summary>Well-known role names (mirrors SimplCommerce's seeded "admin" role).</summary>
public static class AppRoles
{
    public const string Admin = "admin";

    /// <summary>Storefront shoppers; assigned when a customer is created.</summary>
    public const string Customer = "customer";
}
