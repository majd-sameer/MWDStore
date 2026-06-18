namespace Store.Application.Catalog.Models;

/// <summary>
/// Port of the storefront listing view models (<c>ProductsByCategory</c> / <c>SearchResult</c>):
/// the paged product set plus facets and the (clamped) paging state.
/// </summary>
public sealed class ProductListResult
{
    public IList<ProductListItem> Products { get; set; } = new List<ProductListItem>();
    public int TotalProduct { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public FilterOption FilterOption { get; set; } = new();
}
