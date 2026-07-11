using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Fills in product images for products that have no <see cref="ProductMedium"/> rows yet — the gap
/// left by <see cref="CatalogSeeder"/>, which creates products without wiring any media (see its
/// doc comment). Two sources, tried in order per product:
///
///  1. <b>Local dump folder</b> (<c>Store.Api/seed-images/</c>) — the future PSD photo dump. Supports
///     <c>seed-images/&lt;slug&gt;.&lt;ext&gt;</c> (single image) and
///     <c>seed-images/&lt;slug&gt;/&lt;anything&gt;.&lt;ext&gt;</c> (multiple, ordered by filename).
///     Recognised extensions: jpg/jpeg/png/webp. Folder absent (the common case today) is a silent
///     no-op — it's a placeholder for a photo dump that doesn't exist yet.
///  2. <b>Seed URLs</b> — for products the dump didn't cover, downloads the <c>images</c> URLs from
///     <c>catalog.seed.json</c> via <see cref="IMediaDownloader"/> (bounded concurrency). A failed
///     download is logged and the product is simply left imageless — never a boot-time failure — and
///     will be retried on the next boot since it still has zero <see cref="ProductMedium"/> rows.
///
/// Both sources save through the same <see cref="IMediaStorage"/>/<see cref="Medium"/>/
/// <see cref="ProductMedium"/> plumbing as an admin image upload (GUID-named files under
/// <c>user-content/</c>), so seeded images are indistinguishable from admin-uploaded ones.
///
/// Idempotent and additive: only products with zero <see cref="ProductMedium"/> rows are touched, so
/// a partial failure (missing dump files, failed downloads, no internet) simply resumes on the next
/// boot. Disable entirely via the <c>Media:SeedImages</c> config switch (default <c>true</c>).
/// </summary>
public static class MediaSeeder
{
    private const int MaxConcurrentDownloads = 8;
    private const string DumpFolderName = "seed-images";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> DumpExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("MediaSeeder");
        var config = sp.GetRequiredService<IConfiguration>();

        if (!(config.GetValue<bool?>("Media:SeedImages") ?? true))
        {
            logger.LogInformation("Media:SeedImages is disabled — skipping product image seeding.");
            return;
        }

        var db = sp.GetRequiredService<StoreDbContext>();

        // Cheap bail-out for the common case (this machine's fully-seeded DB): a single count query
        // instead of loading rows, so boot stays fast once every product already has media.
        var missingCount = await db.Products.CountAsync(p => !p.ProductMedia.Any(), cancellationToken);
        if (missingCount == 0)
        {
            logger.LogInformation("All products already have media — skipping image seeding.");
            return;
        }

        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var storage = sp.GetRequiredService<IMediaStorage>();

        var candidates = await db.Products
            .Where(p => !p.ProductMedia.Any())
            .Select(p => new { p.Id, p.Slug })
            .ToListAsync(cancellationToken);

        // 1) Local dump folder.
        var dumpRoot = Path.Combine(env.ContentRootPath, DumpFolderName);
        var localized = 0;
        var remaining = new List<(long Id, string Slug)>();
        if (Directory.Exists(dumpRoot))
        {
            foreach (var candidate in candidates)
            {
                var files = ResolveDumpFiles(dumpRoot, candidate.Slug);
                if (files.Count == 0)
                {
                    remaining.Add((candidate.Id, candidate.Slug));
                    continue;
                }

                await SeedProductFromFilesAsync(db, storage, candidate.Id, files, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                localized++;
            }
        }
        else
        {
            remaining.AddRange(candidates.Select(c => (c.Id, c.Slug)));
        }

        // 2) Seed URLs from catalog.seed.json, for whatever the dump didn't cover.
        var downloaded = 0;
        if (remaining.Count > 0)
        {
            var seedPath = Path.Combine(env.ContentRootPath, "catalog.seed.json");
            if (!File.Exists(seedPath))
            {
                logger.LogInformation(
                    "catalog.seed.json not found at {Path} — cannot download seed images for {Count} product(s).",
                    seedPath, remaining.Count);
            }
            else
            {
                var imagesBySlug = await LoadSeedImagesBySlugAsync(seedPath, cancellationToken);
                var downloader = sp.GetRequiredService<IMediaDownloader>();
                downloaded = await DownloadRemainingAsync(db, storage, downloader, remaining, imagesBySlug, logger, cancellationToken);
            }
        }

        logger.LogInformation(
            "Media seed done: {Localized} product(s) from local dump, {Downloaded} from seed URLs ({StillMissing} still imageless).",
            localized, downloaded, missingCount - localized - downloaded);
    }

    /// <summary>Resolves the dump files for one product's slug: prefer the multi-image folder
    /// convention, ordered by filename, then fall back to a single <c>slug.ext</c> file.</summary>
    private static List<string> ResolveDumpFiles(string dumpRoot, string slug)
    {
        var folder = Path.Combine(dumpRoot, slug);
        if (Directory.Exists(folder))
        {
            return Directory.EnumerateFiles(folder)
                .Where(f => DumpExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        foreach (var ext in DumpExtensions)
        {
            var file = Path.Combine(dumpRoot, slug + ext);
            if (File.Exists(file))
            {
                return [file];
            }
        }

        return [];
    }

    /// <summary>Attaches local files to a product: one <see cref="Medium"/> + <see cref="ProductMedium"/>
    /// per file (ordered as given), thumbnail set to the first. Saves through the same
    /// <see cref="IMediaStorage"/> convention as an admin upload (GUID file name, original extension).</summary>
    private static async Task SeedProductFromFilesAsync(
        StoreDbContext db, IMediaStorage storage, long productId, List<string> files, CancellationToken cancellationToken)
    {
        var product = await db.Products.Include(p => p.ProductMedia).FirstAsync(p => p.Id == productId, cancellationToken);

        var order = 0;
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var storedFileName = await storage.SaveAsync(stream, Path.GetFileName(file), cancellationToken);
            var medium = new Medium
            {
                FileName = storedFileName,
                FileSize = (int)new FileInfo(file).Length,
                MediaType = MediaTypes.Image
            };
            product.ProductMedia.Add(new ProductMedium { Media = medium, DisplayOrder = order++ });
            product.ThumbnailImage ??= medium;
        }
    }

    /// <summary>Reads just the slug/images pairs out of catalog.seed.json (a subset of the schema
    /// CatalogSeeder itself parses — extra fields are ignored by System.Text.Json by default).</summary>
    private static async Task<Dictionary<string, List<string>>> LoadSeedImagesBySlugAsync(
        string seedPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonOptions, cancellationToken);
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in seed?.Products ?? [])
        {
            result[product.Slug] = product.Images;
        }

        return result;
    }

    /// <summary>Downloads each remaining product's seed image URLs with bounded concurrency
    /// (<see cref="MaxConcurrentDownloads"/> in flight across all products/urls at once), then
    /// persists successes sequentially against the (single-threaded) <see cref="StoreDbContext"/>.</summary>
    private static async Task<int> DownloadRemainingAsync(
        StoreDbContext db,
        IMediaStorage storage,
        IMediaDownloader downloader,
        List<(long Id, string Slug)> remaining,
        Dictionary<string, List<string>> imagesBySlug,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(MaxConcurrentDownloads);

        var downloadTasks = remaining.Select(async product =>
        {
            if (!imagesBySlug.TryGetValue(product.Slug, out var urls) || urls.Count == 0)
            {
                return (product.Id, product.Slug, Files: new List<(byte[] Bytes, string Url)>());
            }

            var files = new List<(byte[] Bytes, string Url)>();
            foreach (var url in urls)
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    var bytes = await downloader.DownloadAsync(url, cancellationToken);
                    if (bytes is { Length: > 0 })
                    {
                        files.Add((bytes, url));
                    }
                    else
                    {
                        logger.LogWarning("Failed to download seed image {Url} for product '{Slug}'.", url, product.Slug);
                    }
                }
                finally
                {
                    throttle.Release();
                }
            }

            return (product.Id, product.Slug, Files: files);
        });

        var results = await Task.WhenAll(downloadTasks);

        var downloaded = 0;
        foreach (var (id, _, files) in results)
        {
            if (files.Count == 0)
            {
                continue;
            }

            var product = await db.Products.Include(p => p.ProductMedia).FirstAsync(p => p.Id == id, cancellationToken);
            var order = 0;
            foreach (var (bytes, url) in files)
            {
                await using var stream = new MemoryStream(bytes);
                var storedFileName = await storage.SaveAsync(stream, ExtensionOnlyFileName(url), cancellationToken);
                var medium = new Medium { FileName = storedFileName, FileSize = bytes.Length, MediaType = MediaTypes.Image };
                product.ProductMedia.Add(new ProductMedium { Media = medium, DisplayOrder = order++ });
                product.ThumbnailImage ??= medium;
            }

            await db.SaveChangesAsync(cancellationToken);
            downloaded++;
        }

        return downloaded;
    }

    /// <summary>Extracts the URL's path extension so <see cref="IMediaStorage.SaveAsync"/> (which
    /// keeps whatever extension the given file name has) stores the right one; defaults to .png when
    /// the URL has no recognizable extension.</summary>
    private static string ExtensionOnlyFileName(string url)
    {
        string ext;
        try
        {
            ext = Path.GetExtension(new Uri(url).AbsolutePath);
        }
        catch (UriFormatException)
        {
            ext = "";
        }

        if (string.IsNullOrEmpty(ext))
        {
            ext = ".png";
        }

        return "seed" + ext;
    }

    private sealed record SeedFile
    {
        public List<SeedProduct> Products { get; init; } = [];
    }

    private sealed record SeedProduct
    {
        public required string Slug { get; init; }
        public List<string> Images { get; init; } = [];
    }
}
