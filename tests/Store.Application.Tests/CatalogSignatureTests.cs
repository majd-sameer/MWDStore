using Store.Application.Catalog;
using Store.Application.Catalog.Models;
using Store.Application.Catalog.Pricing;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Signature-product catalog behavior (Phase 4): the dedicated endpoint returns only
/// published + individually-visible flagged products in sort order, the default listing sort boosts
/// signature products first, and an explicit price sort ignores that boost.
/// </summary>
public class CatalogSignatureTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static CatalogService NewService(StoreDbContext db) =>
        new(db, new ProductPricingService(new FixedTimeProvider(Now)),
            new CatalogOptions { ProductPageSize = 20 },
            new Store.Application.Common.LocalMediaUrlBuilder());

    private static void Add(
        StoreDbContext db, long id, string name, decimal price,
        bool signature = false, int signatureSort = 0, bool published = true, bool visible = true)
    {
        db.Products.Add(new Product
        {
            Id = id,
            Name = name,
            Slug = "p" + id,
            Price = price,
            IsPublished = published,
            IsVisibleIndividually = visible,
            IsAllowToOrder = true,
            StockQuantity = 10,
            IsSignature = signature,
            SignatureSortOrder = signatureSort,
            CreatedById = 1,
            LatestUpdatedById = 1,
        });
    }

    private static StoreDbContext Seed()
    {
        var db = TestDb.New();
        Add(db, 1, "Regular", 100m);
        Add(db, 2, "Signature Later", 300m, signature: true, signatureSort: 1);
        Add(db, 3, "Signature First", 50m, signature: true, signatureSort: 0);
        Add(db, 4, "Signature Unpublished", 80m, signature: true, published: false);
        Add(db, 5, "Signature Hidden", 80m, signature: true, visible: false);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Signature_endpoint_returns_only_published_visible_flagged_in_order()
    {
        using var db = Seed();

        var items = await NewService(db).GetSignatureProductsAsync(10);

        // 3 (sort 0) then 2 (sort 1); the unpublished/hidden signature products are excluded.
        Assert.Equal(new long[] { 3, 2 }, items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task Default_sort_boosts_signature_products_first()
    {
        using var db = Seed();

        var result = await NewService(db).SearchAsync(new ProductListOptions { Page = 1, PageSize = 20 });

        Assert.Equal(new long[] { 3, 2, 1 }, result.Products.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Explicit_price_sort_ignores_signature_boost()
    {
        using var db = Seed();

        var result = await NewService(db).SearchAsync(
            new ProductListOptions { Page = 1, PageSize = 20, Sort = "price-asc" });

        // Pure ascending price: 3 (50), 1 (100), 2 (300). The non-signature product sitting between
        // the two signatures proves the boost was not applied to an explicit sort.
        Assert.Equal(new long[] { 3, 1, 2 }, result.Products.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task List_items_carry_the_signature_flag()
    {
        using var db = Seed();

        var result = await NewService(db).SearchAsync(new ProductListOptions { Page = 1, PageSize = 20 });

        Assert.True(result.Products.Single(p => p.Id == 2).IsSignature);
        Assert.False(result.Products.Single(p => p.Id == 1).IsSignature);
    }
}
