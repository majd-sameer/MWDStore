using Store.Application.Catalog.Models;

namespace Store.Application.Catalog;

/// <summary>
/// Storefront catalog reads: product listing with search/filter/sort, and product detail with
/// variations/attributes.
/// </summary>
public interface ICatalogService
{
    /// <summary>Published, individually-visible products in a category.</summary>
    Task<ProductListResult> GetProductsByCategoryAsync(
        long categoryId, ProductListOptions options, CancellationToken cancellationToken = default);

    /// <summary>Full-text product search with filter/sort; an empty query browses the full catalog.</summary>
    Task<ProductListResult> SearchAsync(
        ProductListOptions options, CancellationToken cancellationToken = default);

    /// <summary>Published, individually-visible signature products ordered by their sort order (home rail).</summary>
    Task<IList<ProductListItem>> GetSignatureProductsAsync(
        int take, CancellationToken cancellationToken = default);

    /// <summary>Product detail with attributes, variations and related products.</summary>
    Task<ProductDetailModel?> GetProductDetailAsync(
        long id, CancellationToken cancellationToken = default);
}
