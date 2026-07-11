using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Store.Api.Infrastructure;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>Covers <see cref="CatalogSeeder"/>'s bilingual seed schema: a <c>catalog.seed.json</c>
/// entry that carries a <c>nameEn</c>/<c>*En</c> field lands in the entity's <c>LocalizedString.En</c>,
/// while the current production file (no <c>*En</c> fields at all) keeps seeding pure-Arabic
/// <see cref="LocalizedString"/> values with <c>En == null</c> — idempotence and behavior for the
/// existing file are unchanged.</summary>
public class CatalogSeederTests
{
    /// <summary>Minimal <see cref="IWebHostEnvironment"/> fake pointing ContentRootPath at a temp
    /// directory holding a test-authored catalog.seed.json.</summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Store.Application.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider BuildProvider(StoreDbContext db, string seedJson)
    {
        var dir = Directory.CreateTempSubdirectory("catalog-seeder-tests-");
        File.WriteAllText(Path.Combine(dir.FullName, "catalog.seed.json"), seedJson);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment { ContentRootPath = dir.FullName });
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static void SeedOwner(StoreDbContext db)
    {
        // No Warehouse row: CatalogSeeder tolerates that (logs a warning, skips Stock rows) —
        // avoids needing a full Warehouse/Address graph just to exercise the bilingual mapping.
        db.Users.Add(new User { Id = 1, UserName = "owner@example.com", Email = "owner@example.com", FullName = "Owner" });
        db.SaveChanges();
    }

    private const string SeedWithEnglish = """
        {
          "categories": [
            { "slug": "textiles", "name": "منسوجات", "nameEn": "Textiles", "parent": null, "displayOrder": 1 }
          ],
          "products": [
            {
              "slug": "prod-en",
              "sku": "SKU-EN",
              "name": "قميص",
              "nameEn": "Shirt",
              "price": 10,
              "shortDescription": "وصف قصير",
              "shortDescriptionEn": "Short description",
              "description": "وصف طويل",
              "descriptionEn": "Long description",
              "stock": 5,
              "categories": ["textiles"],
              "images": [],
              "isFeatured": false
            }
          ]
        }
        """;

    private const string SeedWithoutEnglish = """
        {
          "categories": [
            { "slug": "textiles", "name": "منسوجات", "parent": null, "displayOrder": 1 }
          ],
          "products": [
            {
              "slug": "prod-ar",
              "sku": "SKU-AR",
              "name": "قميص",
              "price": 10,
              "shortDescription": "وصف قصير",
              "description": "وصف طويل",
              "stock": 5,
              "categories": ["textiles"],
              "images": [],
              "isFeatured": false
            }
          ]
        }
        """;

    [Fact]
    public async Task SeedAsync_with_En_fields_populates_LocalizedString_En()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var provider = BuildProvider(db, SeedWithEnglish);

        await CatalogSeeder.SeedAsync(provider);

        var category = db.Categories.Single(c => c.Slug == "textiles");
        Assert.Equal("منسوجات", category.Name.Ar);
        Assert.Equal("Textiles", category.Name.En);

        var product = db.Products.Single(p => p.Slug == "prod-en");
        Assert.Equal("قميص", product.Name.Ar);
        Assert.Equal("Shirt", product.Name.En);
        Assert.Equal("Short description", product.ShortDescription?.En);
        Assert.Equal("Long description", product.Description?.En);
    }

    [Fact]
    public async Task SeedAsync_without_En_fields_leaves_En_null()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var provider = BuildProvider(db, SeedWithoutEnglish);

        await CatalogSeeder.SeedAsync(provider);

        var category = db.Categories.Single(c => c.Slug == "textiles");
        Assert.Equal("منسوجات", category.Name.Ar);
        Assert.Null(category.Name.En);

        var product = db.Products.Single(p => p.Slug == "prod-ar");
        Assert.Equal("قميص", product.Name.Ar);
        Assert.Null(product.Name.En);
        Assert.NotNull(product.ShortDescription);
        Assert.Null(product.ShortDescription!.En);
        Assert.NotNull(product.Description);
        Assert.Null(product.Description!.En);
    }
}
