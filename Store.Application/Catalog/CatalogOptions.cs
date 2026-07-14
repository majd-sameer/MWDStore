namespace Store.Application.Catalog;

/// <summary>
/// Catalog configuration. <see cref="ProductPageSize"/> is the server-controlled default page size.
/// </summary>
public sealed class CatalogOptions
{
    public int ProductPageSize { get; set; } = 10;
}
