using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Store.Api.Infrastructure;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>Covers <see cref="MediaSeeder"/>'s two-source product-image import: a local
/// <c>seed-images/</c> dump (single file and multi-image folder conventions), the
/// <c>catalog.seed.json</c>-URL download fallback via a fake <see cref="IMediaDownloader"/>, and
/// idempotence (only products with zero <see cref="ProductMedium"/> rows are ever touched).</summary>
public class MediaSeederTests
{
    /// <summary>Same minimal <see cref="IWebHostEnvironment"/> fake as <c>CatalogSeederTests</c>,
    /// pointing ContentRootPath at a temp directory that holds catalog.seed.json / seed-images/ /
    /// user-content for the duration of one test.</summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Store.Application.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeMediaDownloader : IMediaDownloader
    {
        private readonly Func<string, byte[]?> _handler;

        public FakeMediaDownloader(Func<string, byte[]?> handler) => _handler = handler;

        public List<string> RequestedUrls { get; } = [];

        public Task<byte[]?> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            RequestedUrls.Add(url);
            return Task.FromResult(_handler(url));
        }
    }

    private static ServiceProvider BuildProvider(
        StoreDbContext db, string contentRoot, IMediaDownloader downloader, bool seedImages = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment { ContentRootPath = contentRoot });
        services.AddSingleton<IMediaStorage>(new LocalMediaStorage(new FakeWebHostEnvironment { ContentRootPath = contentRoot }));
        services.AddSingleton(downloader);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection([new("Media:SeedImages", seedImages ? "true" : "false")])
            .Build());
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static Product AddProduct(StoreDbContext db, string slug, long ownerId = 1)
    {
        var product = new Product
        {
            Name = new LocalizedString(slug),
            Slug = slug,
            NormalizedName = slug.ToUpperInvariant(),
            CreatedById = ownerId,
            CreatedOn = DateTimeOffset.UtcNow,
            LatestUpdatedById = ownerId,
            LatestUpdatedOn = DateTimeOffset.UtcNow
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    private static void SeedOwner(StoreDbContext db)
    {
        db.Users.Add(new User { Id = 1, UserName = "owner@example.com", Email = "owner@example.com", FullName = "Owner" });
        db.SaveChanges();
    }

    private static string NewContentRoot()
    {
        return Directory.CreateTempSubdirectory("media-seeder-tests-").FullName;
    }

    // ----- Source 1: local dump folder --------------------------------------------------------

    [Fact]
    public async Task Dump_single_file_attaches_media_and_thumbnail()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var product = AddProduct(db, "rug-1");

        var root = NewContentRoot();
        var dumpDir = Path.Combine(root, "seed-images");
        Directory.CreateDirectory(dumpDir);
        File.WriteAllBytes(Path.Combine(dumpDir, "rug-1.jpg"), [1, 2, 3]);

        var downloader = new FakeMediaDownloader(_ => throw new InvalidOperationException("should not be called"));
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);

        var reloaded = db.Products.Single(p => p.Id == product.Id);
        var media = db.ProductMedia.Where(pm => pm.ProductId == product.Id).ToList();
        Assert.Single(media);
        Assert.NotNull(reloaded.ThumbnailImageId);
        Assert.Equal(media[0].MediaId, reloaded.ThumbnailImageId);

        var storedFileName = db.Media.Single(m => m.Id == media[0].MediaId).FileName!;
        Assert.EndsWith(".jpg", storedFileName);
        Assert.True(File.Exists(Path.Combine(root, "user-content", storedFileName)));
        Assert.Empty(downloader.RequestedUrls);
    }

    [Fact]
    public async Task Dump_multi_image_folder_is_ordered_by_filename()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var product = AddProduct(db, "rug-2");

        var root = NewContentRoot();
        var dumpDir = Path.Combine(root, "seed-images", "rug-2");
        Directory.CreateDirectory(dumpDir);
        // Distinct byte *lengths* (not values) so FileSize can double as an order marker below —
        // written out of alphabetical order to prove sorting isn't just creation order.
        File.WriteAllBytes(Path.Combine(dumpDir, "b.png"), [1, 1]);
        File.WriteAllBytes(Path.Combine(dumpDir, "a.png"), [1]);
        File.WriteAllBytes(Path.Combine(dumpDir, "c.png"), [1, 1, 1]);

        var downloader = new FakeMediaDownloader(_ => null);
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);

        var media = db.ProductMedia.Where(pm => pm.ProductId == product.Id).OrderBy(pm => pm.DisplayOrder).ToList();
        Assert.Equal(3, media.Count);

        // Ordered by filename (a, b, c), not creation order (b, a, c).
        var fileNames = media
            .Select(pm => db.Media.Single(m => m.Id == pm.MediaId).FileSize)
            .ToList();
        Assert.Equal([1, 2, 3], fileNames); // FileSize doubles as a marker for which source byte(s) it was.

        var reloaded = db.Products.Single(p => p.Id == product.Id);
        Assert.Equal(media[0].MediaId, reloaded.ThumbnailImageId);
    }

    [Fact]
    public async Task Product_with_existing_media_is_left_untouched_by_dump()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var product = AddProduct(db, "rug-3");
        var existingMedium = new Medium { FileName = "existing.jpg", MediaType = MediaTypes.Image };
        db.Media.Add(existingMedium);
        db.SaveChanges();
        db.ProductMedia.Add(new ProductMedium { ProductId = product.Id, MediaId = existingMedium.Id, DisplayOrder = 0 });
        db.SaveChanges();

        var root = NewContentRoot();
        var dumpDir = Path.Combine(root, "seed-images");
        Directory.CreateDirectory(dumpDir);
        File.WriteAllBytes(Path.Combine(dumpDir, "rug-3.jpg"), [9, 9, 9]);

        var downloader = new FakeMediaDownloader(_ => null);
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);

        var media = db.ProductMedia.Where(pm => pm.ProductId == product.Id).ToList();
        Assert.Single(media);
        Assert.Equal(existingMedium.Id, media[0].MediaId);
    }

    [Fact]
    public async Task Missing_dump_folder_is_a_silent_noop_and_falls_through_to_urls()
    {
        var db = TestDb.New();
        SeedOwner(db);
        AddProduct(db, "rug-4");

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-4", ["https://example.test/rug-4.png"]));

        var downloader = new FakeMediaDownloader(_ => [7, 7, 7]);
        var provider = BuildProvider(db, root, downloader);

        // No seed-images/ directory created at all.
        await MediaSeeder.SeedAsync(provider);

        Assert.Single(downloader.RequestedUrls);
        Assert.Single(db.ProductMedia);
    }

    // ----- Source 2: seed URL download ---------------------------------------------------------

    [Fact]
    public async Task Url_source_downloads_and_attaches_media_for_imageless_product()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var product = AddProduct(db, "rug-5");

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-5", ["https://example.test/media/rug-5.webp"]));

        var downloader = new FakeMediaDownloader(url => url.EndsWith("rug-5.webp") ? [1, 2, 3, 4] : null);
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);

        var media = db.ProductMedia.Where(pm => pm.ProductId == product.Id).ToList();
        Assert.Single(media);
        var stored = db.Media.Single(m => m.Id == media[0].MediaId);
        Assert.EndsWith(".webp", stored.FileName);
        Assert.Equal(4, stored.FileSize);

        var reloaded = db.Products.Single(p => p.Id == product.Id);
        Assert.Equal(media[0].MediaId, reloaded.ThumbnailImageId);
    }

    [Fact]
    public async Task Url_source_download_failure_skips_product_without_throwing()
    {
        var db = TestDb.New();
        SeedOwner(db);
        AddProduct(db, "rug-6");

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-6", ["https://example.test/media/rug-6.png"]));

        var downloader = new FakeMediaDownloader(_ => null); // simulates a failed/unreachable download
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider); // must not throw

        Assert.Empty(db.ProductMedia);
    }

    [Fact]
    public async Task All_products_have_media_skips_without_calling_downloader()
    {
        var db = TestDb.New();
        SeedOwner(db);
        var product = AddProduct(db, "rug-7");
        var existingMedium = new Medium { FileName = "existing.jpg", MediaType = MediaTypes.Image };
        db.Media.Add(existingMedium);
        db.SaveChanges();
        db.ProductMedia.Add(new ProductMedium { ProductId = product.Id, MediaId = existingMedium.Id, DisplayOrder = 0 });
        db.SaveChanges();

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-7", ["https://example.test/media/rug-7.png"]));

        var downloader = new FakeMediaDownloader(_ => throw new InvalidOperationException("should not be called"));
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);

        Assert.Empty(downloader.RequestedUrls);
    }

    [Fact]
    public async Task Config_switch_disables_seeding_entirely()
    {
        var db = TestDb.New();
        SeedOwner(db);
        AddProduct(db, "rug-8");

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-8", ["https://example.test/media/rug-8.png"]));

        var downloader = new FakeMediaDownloader(_ => throw new InvalidOperationException("should not be called"));
        var provider = BuildProvider(db, root, downloader, seedImages: false);

        await MediaSeeder.SeedAsync(provider);

        Assert.Empty(db.ProductMedia);
        Assert.Empty(downloader.RequestedUrls);
    }

    // ----- Idempotence ---------------------------------------------------------------------------

    [Fact]
    public async Task Second_run_is_a_noop()
    {
        var db = TestDb.New();
        SeedOwner(db);
        AddProduct(db, "rug-9");

        var root = NewContentRoot();
        WriteCatalogSeedJson(root, ("rug-9", ["https://example.test/media/rug-9.png"]));

        var downloader = new FakeMediaDownloader(_ => [5, 5, 5]);
        var provider = BuildProvider(db, root, downloader);

        await MediaSeeder.SeedAsync(provider);
        Assert.Single(downloader.RequestedUrls);
        Assert.Single(db.ProductMedia);

        // Second boot: the product already has media, so the downloader must not be called again.
        var provider2 = BuildProvider(db, root, downloader);
        await MediaSeeder.SeedAsync(provider2);

        Assert.Single(downloader.RequestedUrls); // unchanged
        Assert.Single(db.ProductMedia); // unchanged
    }

    private static void WriteCatalogSeedJson(string contentRoot, params (string Slug, string[] Images)[] products)
    {
        var productsJson = string.Join(",\n", products.Select(p => $$"""
            {
              "slug": "{{p.Slug}}",
              "sku": "SKU-{{p.Slug}}",
              "name": "{{p.Slug}}",
              "price": 10,
              "stock": 5,
              "categories": [],
              "images": [{{string.Join(",", p.Images.Select(i => $"\"{i}\""))}}],
              "isFeatured": false
            }
            """));

        var json = $$"""
            {
              "categories": [],
              "products": [
                {{productsJson}}
              ]
            }
            """;

        File.WriteAllText(Path.Combine(contentRoot, "catalog.seed.json"), json);
    }
}
