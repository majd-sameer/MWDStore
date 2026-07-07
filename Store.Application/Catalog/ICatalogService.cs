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

    /// <summary>Published, individually-visible signature products ordered by their sort order (home rail).</summary>
    Task<IList<ProductListItem>> GetSignatureProductsAsync(
        int take, CancellationToken cancellationToken = default);

    /// <summary>Product detail with attributes and variations — port of <c>ProductController.ProductDetail</c>.</summary>
    Task<ProductDetailModel?> GetProductDetailAsync(
        long id, CancellationToken cancellationToken = default);
}
