namespace Store.Application.Catalog;

/// <summary>
/// Catalog configuration. <see cref="ProductPageSize"/> corresponds to SimplCommerce's
/// <c>Catalog.ProductPageSize</c> app setting (page size is server-controlled, not client-supplied).
/// </summary>
public sealed class CatalogOptions
{
    public int ProductPageSize { get; set; } = 10;
}
