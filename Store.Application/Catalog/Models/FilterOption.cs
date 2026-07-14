namespace Store.Application.Catalog.Models;

/// <summary>
/// Listing facets, computed over the unfiltered base query.
/// </summary>
public sealed class FilterOption
{
    public FilterPrice Price { get; set; } = new();
    public IList<FilterCategory> Categories { get; set; } = new List<FilterCategory>();
    public IList<FilterBrand> Brands { get; set; } = new List<FilterBrand>();
}

public sealed class FilterPrice
{
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

public sealed class FilterCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public int Count { get; set; }
}

public sealed class FilterBrand
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Count { get; set; }
}
