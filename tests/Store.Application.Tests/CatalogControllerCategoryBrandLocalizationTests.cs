using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers;
using Store.Api.Models;
using Store.Application.Catalog;
using Store.Application.Catalog.Models;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the public storefront read localization on <see cref="CatalogController.Categories"/>
/// and <see cref="CatalogController.Brands"/>: the English name resolves when the request culture is
/// <see cref="ContentLanguage.English"/>, and the Arabic base column is served unchanged otherwise.
/// </summary>
public class CatalogControllerCategoryBrandLocalizationTests
{
    /// <summary>Categories/Brands don't touch <see cref="ICatalogService"/> — a throwing fake keeps
    /// the controller's real constructor shape without needing a fully wired catalog service.</summary>
    private sealed class NotUsedCatalogService : ICatalogService
    {
        public Task<ProductListResult> GetProductsByCategoryAsync(
            long categoryId, ProductListOptions options, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ProductListResult> SearchAsync(ProductListOptions options, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ProductDetailModel?> GetProductDetailAsync(
            long id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static CatalogController NewController(StoreDbContext db, IRequestCulture culture)
    {
        var controller = new CatalogController(
            new NotUsedCatalogService(), db, new FixedTimeProvider(DateTimeOffset.UtcNow), culture);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static IReadOnlyList<T> Body<T>(ActionResult<IReadOnlyList<T>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<List<T>>(ok.Value);
    }

    private static StoreDbContext SeedFixture()
    {
        var db = TestDb.New();

        db.Categories.Add(new Category
        {
            Id = 1, Name = new LocalizedString("أدوات المطبخ", "Kitchen Tools"), Slug = "kitchen-tools",
            IsPublished = true, DisplayOrder = 1,
        });
        db.Brands.Add(new Brand
        {
            Id = 1, Name = new LocalizedString("علامة تجارية", "The Brand"), Slug = "the-brand", IsPublished = true,
        });

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Categories_WithEnglishCulture_ReturnsEnglishName()
    {
        using var db = SeedFixture();
        var controller = NewController(db, TestCulture.English);

        var categories = Body(await controller.Categories(default));

        Assert.Equal("Kitchen Tools", Assert.Single(categories).Name);
    }

    [Fact]
    public async Task Categories_WithArabicCulture_ReturnsBaseArabicName()
    {
        using var db = SeedFixture();
        var controller = NewController(db, TestCulture.Arabic);

        var categories = Body(await controller.Categories(default));

        Assert.Equal("أدوات المطبخ", Assert.Single(categories).Name);
    }

    [Fact]
    public async Task Brands_WithEnglishCulture_ReturnsEnglishName()
    {
        using var db = SeedFixture();
        var controller = NewController(db, TestCulture.English);

        var brands = Body(await controller.Brands(default));

        Assert.Equal("The Brand", Assert.Single(brands).Name);
    }

    [Fact]
    public async Task Brands_WithArabicCulture_ReturnsBaseArabicName()
    {
        using var db = SeedFixture();
        var controller = NewController(db, TestCulture.Arabic);

        var brands = Body(await controller.Brands(default));

        Assert.Equal("علامة تجارية", Assert.Single(brands).Name);
    }

    [Fact]
    public async Task Categories_FallsBackToBaseName_WhenNoTranslationExists()
    {
        using var db = SeedFixture();
        db.Categories.Add(new Category
        {
            Id = 2, Name = new LocalizedString("غير مترجم"), Slug = "untranslated", IsPublished = true, DisplayOrder = 2,
        });
        db.SaveChanges();
        var controller = NewController(db, TestCulture.English);

        var categories = Body(await controller.Categories(default));

        var untranslated = categories.Single(c => c.Id == 2);
        Assert.Equal("غير مترجم", untranslated.Name);
    }
}
