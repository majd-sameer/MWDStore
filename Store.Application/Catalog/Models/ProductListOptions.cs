namespace Store.Application.Catalog.Models;

/// <summary>
/// The query/filter/sort/paging inputs shared by the category, brand and search listings.
/// </summary>
public sealed class ProductListOptions
{
    public string? Query { get; set; }

    /// <summary>"--"-separated brand slugs.</summary>
    public string? Brand { get; set; }

    /// <summary>"--"-separated category slugs.</summary>
    public string? Category { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? Sort { get; set; }

    public int? MinPrice { get; set; }

    public int? MaxPrice { get; set; }

    /// <summary>Keep only products whose average rating is at least this value (e.g. 4 or 4.5).</summary>
    public double? MinRating { get; set; }

    public IList<string> GetBrands() =>
        string.IsNullOrWhiteSpace(Brand)
            ? new List<string>()
            : Brand.Split(["--"], StringSplitOptions.RemoveEmptyEntries).ToList();

    public IList<string> GetCategories() =>
        string.IsNullOrWhiteSpace(Category)
            ? new List<string>()
            : Category.Split(["--"], StringSplitOptions.RemoveEmptyEntries).ToList();
}
