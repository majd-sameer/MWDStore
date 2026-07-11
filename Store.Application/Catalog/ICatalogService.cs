using Store.Application.Catalog.Models;

namespace Store.Application.Catalog;

/// <summary>
/// Storefront catalog read logic ported from SimplCommerce's <c>CategoryController</c>,
/// <c>SearchController</c> and <c>ProductController</c>: product listing with search/filter/sort,
/// and product detail with variations/attributes.
/// </summary>
public interface ICatalogService
{
    /// <summary>Products in a category — port of <c>CategoryController.CategoryDetail</c>.</summary>
    Task<ProductListResult> GetProductsByCategoryAsync(
        long categoryId, ProductListOptions options, CancellationToken cancellationToken = default);

    /// <summary>Full-text product search — port of <c>SearchController.Index</c> (query matching + filter/sort).</summary>
    Task<ProductListResult> SearchAsync(
        ProductListOptions options, CancellationToken cancellationToken = default);

    /// <summary>Product detail with attributes and variations — port of <c>ProductController.ProductDetail</c>.
    /// All localized text (product Name/ShortDescription/Description/Meta*, option/attribute/brand/category
    /// names, variation names, related products) is resolved to the current request language via the
    /// injected <c>IRequestCulture</c>, falling back to Arabic when no English overlay exists.</summary>
    Task<ProductDetailModel?> GetProductDetailAsync(
        long id, CancellationToken cancellationToken = default);
}
