namespace Store.Application.Catalog;

/// <summary>
/// <see cref="Store.Domain.ProductLink.LinkType"/> is stored as a plain <see cref="int"/>,
/// so these constants document the values used by the catalog logic.
/// </summary>
public static class ProductLinkType
{
    public const int Super = 1;
    public const int Related = 2;
    public const int CrossSell = 3;
    public const int UpSell = 4;
}
