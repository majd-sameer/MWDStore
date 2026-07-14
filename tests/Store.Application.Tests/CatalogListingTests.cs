using Store.Application.Catalog;
using Store.Application.Catalog.Models;
using Store.Application.Catalog.Pricing;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Listing behavior: base visibility filter, search matching, price/category/brand
/// filters, sort, pagination clamp and facets.
/// </summary>
public class CatalogListingTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private const long Electronics = 1;
    private const long Phones = 2;
    private const long AcmeId = 1;
    private const long GlobexId = 2;

    private static CatalogService NewService(StoreDbContext db, int pageSize = 10) =>
        new(db, new ProductPricingService(new FixedTimeProvider(Now)), new CatalogOptions { ProductPageSize = pageSize },
            new Store.Application.Common.LocalMediaUrlBuilder());

    // ---- seeding ----------------------------------------------------------

    private static StoreDbContext SeedCategoryFixture()
    {
        var db = TestDb.New();

        var electronics = new Category { Id = Electronics, Name = "Electronics", Slug = "electronics" };
        var phones = new Category { Id = Phones, Name = "Phones", Slug = "phones", ParentId = Electronics };
        var acme = new Brand { Id = AcmeId, Name = "Acme", Slug = "acme", IsPublished = true };
        var globex = new Brand { Id = GlobexId, Name = "Globex", Slug = "globex", IsPublished = true };

        db.Categories.AddRange(electronics, phones);
        db.Brands.AddRange(acme, globex);

        // Visible products in Electronics.
        Add(db, 1, "Alpha Phone", 100m, acme, [electronics, phones]);
        Add(db, 2, "Beta Phone", 200m, globex, [electronics, phones]);
        Add(db, 3, "Gamma TV", 300m, acme, [electronics]);
        Add(db, 4, "Delta Tablet", 50m, globex, [electronics]);
        // Excluded by the base filter.
        Add(db, 5, "Hidden", 999m, acme, [electronics], published: false);
        Add(db, 6, "Not Individually Visible", 5m, globex, [electronics], visible: false);

        db.SaveChanges();
        return db;
    }

    private static void Add(
        StoreDbContext db, long id, string name, decimal price, Brand brand, Category[] categories,
        bool published = true, bool visible = true,
        bool allowToOrder = false, int stockQuantity = 0, bool callForPricing = false,
        bool isFeatured = false)
    {
        var product = new Product
        {
            Id = id,
            Name = name,
            Slug = "p" + id,
            Price = price,
            IsPublished = published,
            IsVisibleIndividually = visible,
            IsAllowToOrder = allowToOrder,
            StockQuantity = stockQuantity,
            IsCallForPricing = callForPricing,
            IsFeatured = isFeatured,
            BrandId = brand.Id,
            Brand = brand,
            CreatedById = 1,
            LatestUpdatedById = 1
        };

        foreach (var category in categories)
        {
            product.ProductCategories.Add(new ProductCategory
            {
                ProductId = id,
                Product = product,
                CategoryId = category.Id,
                Category = category
            });
        }

        db.Products.Add(product);
    }

    private static long[] Ids(ProductListResult result) => result.Products.Select(p => p.Id).ToArray();

    // ---- base filter ------------------------------------------------------

    [Fact]
    public async Task Listing_ExcludesUnpublishedAndNonIndividuallyVisible()
    {
        using var db = SeedCategoryFixture();
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, new ProductListOptions());

        Assert.Equal(4, result.TotalProduct);
        Assert.DoesNotContain(5L, Ids(result)); // unpublished
        Assert.DoesNotContain(6L, Ids(result)); // not individually visible
    }

    // ---- sort -------------------------------------------------------------

    [Fact]
    public async Task Sort_DefaultOrdersFeaturedFirstThenById()
    {
        using var db = TestDb.New();
        var electronics = new Category { Id = Electronics, Name = "Electronics", Slug = "electronics" };
        var acme = new Brand { Id = AcmeId, Name = "Acme", Slug = "acme", IsPublished = true };
        db.Categories.Add(electronics);
        db.Brands.Add(acme);

        Add(db, 1, "Alpha", 100m, acme, [electronics]);
        Add(db, 2, "Beta", 200m, acme, [electronics], isFeatured: true);
        Add(db, 3, "Gamma", 300m, acme, [electronics]);
        db.SaveChanges();

        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, new ProductListOptions());

        // Default sort: featured first, then ascending Id (stable catalog order).
        Assert.Equal([2L, 1L, 3L], Ids(result));
    }

    [Fact]
    public async Task Sort_PriceDesc_OrdersByPriceDescending()
    {
        using var db = SeedCategoryFixture();
        var options = new ProductListOptions { Sort = "price-desc" };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        Assert.Equal([3L, 2L, 1L, 4L], Ids(result)); // 300, 200, 100, 50
    }

    [Fact]
    public async Task Sort_UnknownValue_FallsBackToDefault()
    {
        using var db = SeedCategoryFixture();
        var options = new ProductListOptions { Sort = "whatever" };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        // Unknown sort falls back to the default (featured first, then Id); none are featured here.
        Assert.Equal([1L, 2L, 3L, 4L], Ids(result));
    }

    [Fact]
    public async Task Sort_InStockProductsLeadRegardlessOfSort()
    {
        using var db = TestDb.New();
        var electronics = new Category { Id = Electronics, Name = "Electronics", Slug = "electronics" };
        var acme = new Brand { Id = AcmeId, Name = "Acme", Slug = "acme", IsPublished = true };
        db.Categories.Add(electronics);
        db.Brands.Add(acme);

        // The cheapest item is sold out; the pricier items are in stock; one is call-for-pricing.
        Add(db, 1, "Cheap SoldOut", 10m, acme, [electronics], allowToOrder: true, stockQuantity: 0);
        Add(db, 2, "Mid InStock", 20m, acme, [electronics], allowToOrder: true, stockQuantity: 5);
        Add(db, 3, "Pricey InStock", 30m, acme, [electronics], allowToOrder: true, stockQuantity: 5);
        Add(db, 4, "CallForPricing", 5m, acme, [electronics], callForPricing: true);
        db.SaveChanges();

        var result = await NewService(db).GetProductsByCategoryAsync(
            Electronics, new ProductListOptions { Sort = "price-asc" });

        // Price-asc alone would lead with the sold-out (10) then call-for-pricing (5); instead the
        // available items lead in price order (call-for-pricing counts as available), sold-out trails.
        Assert.Equal([4L, 2L, 3L, 1L], Ids(result));
    }

    // ---- filters ----------------------------------------------------------

    [Fact]
    public async Task Filter_MinAndMaxPrice()
    {
        using var db = SeedCategoryFixture();
        var options = new ProductListOptions { MinPrice = 100, MaxPrice = 200 };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        Assert.Equal([1L, 2L], Ids(result)); // 100, 200
    }

    [Fact]
    public async Task Filter_ByBrandSlug()
    {
        using var db = SeedCategoryFixture();
        var options = new ProductListOptions { Brand = "acme" };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        Assert.Equal([1L, 3L], Ids(result)); // Acme products, price asc
    }

    [Fact]
    public async Task Filter_ByCategorySlug()
    {
        using var db = SeedCategoryFixture();
        var options = new ProductListOptions { Category = "phones" };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        Assert.Equal([1L, 2L], Ids(result)); // only products also in Phones
    }

    // ---- pagination -------------------------------------------------------

    [Fact]
    public async Task Pagination_ReturnsRequestedPage()
    {
        using var db = SeedCategoryFixture();
        var service = NewService(db, pageSize: 2);

        var page1 = await service.GetProductsByCategoryAsync(Electronics, new ProductListOptions { Page = 1 });
        var page2 = await service.GetProductsByCategoryAsync(Electronics, new ProductListOptions { Page = 2 });

        Assert.Equal(4, page1.TotalProduct);
        Assert.Equal([1L, 2L], Ids(page1)); // default order: ascending Id
        Assert.Equal([3L, 4L], Ids(page2));
    }

    [Fact]
    public async Task Pagination_ClampsPageBeyondLastPage()
    {
        using var db = SeedCategoryFixture();
        var service = NewService(db, pageSize: 2);

        var result = await service.GetProductsByCategoryAsync(Electronics, new ProductListOptions { Page = 5 });

        // 4 products, page size 2 -> last valid page is 2.
        Assert.Equal(2, result.Page);
        Assert.Equal([3L, 4L], Ids(result)); // default order: ascending Id
    }

    // ---- facets -----------------------------------------------------------

    [Fact]
    public async Task Facets_PriceRangeAndCounts_ComputedOverBaseQuery()
    {
        using var db = SeedCategoryFixture();
        // Apply a price filter to prove facets are computed over the *unfiltered* base query.
        var options = new ProductListOptions { MinPrice = 100 };
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, options);

        Assert.Equal(50m, result.FilterOption.Price.MinPrice);
        Assert.Equal(300m, result.FilterOption.Price.MaxPrice);

        var electronics = result.FilterOption.Categories.Single(c => c.Slug == "electronics");
        var phones = result.FilterOption.Categories.Single(c => c.Slug == "phones");
        Assert.Equal(4, electronics.Count);
        Assert.Equal(2, phones.Count);

        var acme = result.FilterOption.Brands.Single(b => b.Slug == "acme");
        var globex = result.FilterOption.Brands.Single(b => b.Slug == "globex");
        Assert.Equal(2, acme.Count);
        Assert.Equal(2, globex.Count);
    }

    // ---- pricing projection ----------------------------------------------

    [Fact]
    public async Task Listing_ProjectsCalculatedPrice()
    {
        using var db = SeedCategoryFixture();
        var result = await NewService(db).GetProductsByCategoryAsync(Electronics, new ProductListOptions());

        var item = result.Products.Single(p => p.Id == 1);
        Assert.NotNull(item.CalculatedProductPrice);
        Assert.Equal(100m, item.CalculatedProductPrice!.Price);
        Assert.Null(item.CalculatedProductPrice.OldPrice);
    }

    // ---- search -----------------------------------------------------------

    private static StoreDbContext SeedSearchFixture()
    {
        var db = TestDb.New();
        var electronics = new Category { Id = Electronics, Name = "Electronics", Slug = "electronics" };
        db.Categories.Add(electronics);

        void AddText(long id, string name, decimal price, bool published = true,
            string? shortDesc = null, string? desc = null, string? spec = null)
        {
            var p = new Product
            {
                Id = id, Name = name, Slug = "p" + id, Price = price,
                IsPublished = published, IsVisibleIndividually = true,
                ShortDescription = shortDesc, Description = desc, Specification = spec,
                CreatedById = 1, LatestUpdatedById = 1
            };
            p.ProductCategories.Add(new ProductCategory { ProductId = id, Product = p, CategoryId = Electronics, Category = electronics });
            db.Products.Add(p);
        }

        AddText(1, "Wireless Mouse", 25m, shortDesc: "ergonomic mouse");
        AddText(2, "Gaming Keyboard", 80m, desc: "mechanical keyboard with RGB");
        AddText(3, "USB Cable", 5m, spec: "length: 2m");
        AddText(4, "Monitor", 150m);
        AddText(5, "Keyboard Pro", 120m, published: false); // excluded

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Search_MatchesName()
    {
        using var db = SeedSearchFixture();
        var result = await NewService(db).SearchAsync(new ProductListOptions { Query = "mouse" });

        Assert.Equal([1L], Ids(result));
    }

    [Fact]
    public async Task Search_MatchesDescription_AndIsCaseInsensitive()
    {
        using var db = SeedSearchFixture();
        var result = await NewService(db).SearchAsync(new ProductListOptions { Query = "KEYBOARD" });

        Assert.Equal([2L], Ids(result)); // published keyboard only (id 5 unpublished)
    }

    [Fact]
    public async Task Search_MatchesSpecification()
    {
        using var db = SeedSearchFixture();
        var result = await NewService(db).SearchAsync(new ProductListOptions { Query = "2m" });

        Assert.Equal([3L], Ids(result));
    }

    [Fact]
    public async Task Search_EmptyQuery_BrowsesFullCatalog()
    {
        using var db = SeedSearchFixture();
        var result = await NewService(db).SearchAsync(new ProductListOptions { Query = "   " });

        // A blank query browses the whole catalog —
        // the listing page reuses this endpoint with filters but no search text. Id 5 is unpublished.
        Assert.Equal(4, result.TotalProduct);
        Assert.Equal([1L, 2L, 3L, 4L], Ids(result));
    }
}
