using Store.Application.Catalog;
using Store.Application.Catalog.Pricing;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Product-detail behavior ported from SimplCommerce's <c>ProductController.ProductDetail</c>:
/// attributes, categories, variations (child products linked via <c>ProductLink</c> type Super,
/// options ordered by SortIndex) and related/cross-sell products.
/// </summary>
public class CatalogDetailTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveEnd = new(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private const long ParentId = 10;
    private const long ColorOptionId = 1;
    private const long SizeOptionId = 2;

    private static CatalogService NewService(StoreDbContext db) =>
        new(db, new ProductPricingService(new FixedTimeProvider(Now)), new CatalogOptions(),
            new Store.Application.Common.LocalMediaUrlBuilder());

    private static Product NewProduct(long id, string name, decimal price, bool published = true) => new()
    {
        Id = id,
        Name = name,
        Slug = "p" + id,
        Price = price,
        IsPublished = published,
        IsVisibleIndividually = true,
        IsAllowToOrder = true,
        CreatedById = 1,
        LatestUpdatedById = 1
    };

    private static StoreDbContext SeedProductWithVariations()
    {
        var db = TestDb.New();

        var apparel = new Category { Id = 3, Name = "Apparel", Slug = "apparel" };
        var brand = new Brand { Id = 1, Name = "Acme", Slug = "acme", IsPublished = true };
        var color = new ProductOption { Id = ColorOptionId, Name = "Color" };
        var size = new ProductOption { Id = SizeOptionId, Name = "Size" };
        db.Categories.Add(apparel);
        db.Brands.Add(brand);
        db.ProductOptions.AddRange(color, size);

        // Configurable parent.
        var parent = NewProduct(ParentId, "Config Shirt", 20m);
        parent.HasOptions = true;
        parent.BrandId = brand.Id;
        parent.Brand = brand;
        parent.StockTrackingIsEnabled = true;
        parent.StockQuantity = 5;
        parent.ProductCategories.Add(new ProductCategory { ProductId = ParentId, Product = parent, CategoryId = apparel.Id, Category = apparel });
        parent.ProductAttributeValues.Add(MakeAttr(parent, 1, "Material", "Cotton"));
        parent.ProductAttributeValues.Add(MakeAttr(parent, 2, "Fit", "Slim"));
        db.Products.Add(parent);

        // Variant children. V1 has a live special price.
        var v1 = NewProduct(11, "Shirt Red S", 20m);
        v1.SpecialPrice = 15m;
        v1.SpecialPriceStart = ActiveStart;
        v1.SpecialPriceEnd = ActiveEnd;
        // Add option combinations out of SortIndex order to prove they are reordered.
        v1.ProductOptionCombinations.Add(new ProductOptionCombination { Id = 101, ProductId = 11, Product = v1, OptionId = SizeOptionId, Option = size, Value = "S", SortIndex = 2 });
        v1.ProductOptionCombinations.Add(new ProductOptionCombination { Id = 102, ProductId = 11, Product = v1, OptionId = ColorOptionId, Option = color, Value = "Red", SortIndex = 1 });

        var v2 = NewProduct(12, "Shirt Blue L", 22m);
        v2.ProductOptionCombinations.Add(new ProductOptionCombination { Id = 103, ProductId = 12, Product = v2, OptionId = ColorOptionId, Option = color, Value = "Blue", SortIndex = 1 });
        v2.ProductOptionCombinations.Add(new ProductOptionCombination { Id = 104, ProductId = 12, Product = v2, OptionId = SizeOptionId, Option = size, Value = "L", SortIndex = 2 });

        var v3 = NewProduct(13, "Shirt Hidden", 23m, published: false); // excluded

        db.Products.AddRange(v1, v2, v3);

        // Related + cross-sell products.
        var related = NewProduct(20, "Related Tie", 12m);
        var crossSell = NewProduct(21, "Cross Sell Belt", 18m);
        db.Products.AddRange(related, crossSell);

        // Links owned by the parent.
        db.Set<ProductLink>().AddRange(
            Link(1, parent, v1, ProductLinkType.Super),
            Link(2, parent, v2, ProductLinkType.Super),
            Link(3, parent, v3, ProductLinkType.Super),
            Link(4, parent, related, ProductLinkType.Related),
            Link(5, parent, crossSell, ProductLinkType.CrossSell));

        db.SaveChanges();
        return db;
    }

    private static ProductAttributeValue MakeAttr(Product product, long attrId, string name, string value) => new()
    {
        Id = attrId,
        ProductId = product.Id,
        Product = product,
        AttributeId = attrId,
        Attribute = new ProductAttribute { Id = attrId, Name = name },
        Value = value
    };

    private static ProductLink Link(long id, Product owner, Product linked, int type) => new()
    {
        Id = id,
        ProductId = owner.Id,
        Product = owner,
        LinkedProductId = linked.Id,
        LinkedProduct = linked,
        LinkType = type
    };

    [Fact]
    public async Task Detail_ReturnsNull_WhenNotFoundOrUnpublished()
    {
        using var db = SeedProductWithVariations();
        Assert.Null(await NewService(db).GetProductDetailAsync(999));
        Assert.Null(await NewService(db).GetProductDetailAsync(13)); // exists but unpublished
    }

    [Fact]
    public async Task Detail_MapsAttributesAndCategories()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(ParentId);

        Assert.NotNull(model);
        Assert.Equal("Config Shirt", model!.Name);
        Assert.Equal("acme", model.Brand!.Slug);

        Assert.Equal(
            new[] { ("Material", "Cotton"), ("Fit", "Slim") }.OrderBy(x => x.Item1),
            model.Attributes.Select(a => (a.Name, a.Value!)).OrderBy(x => x.Item1));

        var category = Assert.Single(model.Categories);
        Assert.Equal("apparel", category.Slug);
    }

    [Fact]
    public async Task Detail_IncludesOnlyPublishedVariations()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(ParentId);

        var ids = model!.Variations.Select(v => v.Id).OrderBy(x => x).ToArray();
        Assert.Equal([11L, 12L], ids); // 13 is unpublished
    }

    [Fact]
    public async Task Detail_OrdersVariationOptionsBySortIndex()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(ParentId);

        var v1 = model!.Variations.Single(v => v.Id == 11);
        Assert.Equal(["Color", "Size"], v1.Options.Select(o => o.OptionName).ToArray());
        Assert.Equal(["Red", "S"], v1.Options.Select(o => o.Value!).ToArray());
    }

    [Fact]
    public async Task Detail_ResolvesVariationSpecialPrice()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(ParentId);

        var v1 = model!.Variations.Single(v => v.Id == 11);
        Assert.Equal(15m, v1.CalculatedProductPrice!.Price);
        Assert.Equal(20m, v1.CalculatedProductPrice.OldPrice);
        Assert.Equal(25, v1.CalculatedProductPrice.PercentOfSaving); // 100 - ceil(15/20*100)
    }

    [Fact]
    public async Task Detail_SplitsRelatedAndCrossSellProducts()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(ParentId);

        Assert.Equal([20L], model!.RelatedProducts.Select(p => p.Id).ToArray());
        Assert.Equal([21L], model.CrossSellProducts.Select(p => p.Id).ToArray());
        Assert.Equal(12m, model.RelatedProducts[0].CalculatedProductPrice!.Price);
    }

    [Fact]
    public async Task Detail_NonConfigurableProduct_HasNoVariations()
    {
        using var db = SeedProductWithVariations();
        var model = await NewService(db).GetProductDetailAsync(20); // a leaf product

        Assert.NotNull(model);
        Assert.Empty(model!.Variations);
    }
}
