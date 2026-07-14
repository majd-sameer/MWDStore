using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Data-driven catalog loader fed by <c>catalog.seed.json</c> (generated from the provided product
/// list by <c>Store.Migrator/generate-catalog-seed.mjs</c>). Strictly additive and idempotent:
/// categories and products are looked up by slug and only created when missing — existing rows
/// (including admin edits) are never modified or deleted. A missing seed file is a logged no-op,
/// so the app keeps working without it.
/// </summary>
public static class CatalogSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogSeeder");

        var seedPath = Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "catalog.seed.json");
        if (!File.Exists(seedPath))
        {
            logger.LogInformation("catalog.seed.json not found at {Path} — skipping catalog seeding.", seedPath);
            return;
        }

        CatalogSeed? seed;
        await using (var stream = File.OpenRead(seedPath))
        {
            seed = await JsonSerializer.DeserializeAsync<CatalogSeed>(stream, JsonOptions, cancellationToken);
        }

        if (seed is null || seed.Products.Count == 0)
        {
            logger.LogWarning("catalog.seed.json is empty — nothing to seed.");
            return;
        }

        var db = sp.GetRequiredService<StoreDbContext>();
        var timeProvider = sp.GetRequiredService<TimeProvider>();

        var ownerId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        if (ownerId == 0)
        {
            logger.LogWarning("No user exists yet — skipping catalog seeding (run after IdentitySeeder).");
            return;
        }

        // 1) Categories (idempotent by slug). Parents must be listed before children.
        var seedCategorySlugs = seed.Categories.Select(c => c.Slug).ToList();
        var existingCategories = await db.Categories
            .Where(c => seedCategorySlugs.Contains(c.Slug))
            .ToListAsync(cancellationToken);

        var categoriesBySlug = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var existingCategory in existingCategories)
        {
            categoriesBySlug[existingCategory.Slug] = existingCategory;
        }

        var newCategories = 0;
        foreach (var seedCategory in seed.Categories)
        {
            if (!categoriesBySlug.ContainsKey(seedCategory.Slug))
            {
                long? parentId = null;
                if (seedCategory.Parent is { Length: > 0 } parentSlug)
                {
                    if (!categoriesBySlug.TryGetValue(parentSlug, out var parent))
                    {
                        throw new InvalidOperationException(
                            $"Seed category '{seedCategory.Slug}' references unknown parent '{parentSlug}' (parents must be listed first).");
                    }

                    parentId = parent.Id;
                }

                var category = new Category
                {
                    Name = seedCategory.Name,
                    Slug = seedCategory.Slug,
                    DisplayOrder = seedCategory.DisplayOrder,
                    ParentId = parentId,
                    IsPublished = true,
                    IncludeInMenu = true,
                    IsDeleted = false
                };
                db.Categories.Add(category);
                await db.SaveChangesAsync(cancellationToken);
                newCategories++;

                categoriesBySlug[seedCategory.Slug] = category;
            }
        }

        // 2) Products — insert only the slugs that don't exist yet, as full object graphs
        //    (media + thumbnail + category links + warehouse stock) in batched SaveChanges calls.
        var existingSlugs = (await db.Products.Select(p => p.Slug).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var warehouseId = await db.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => (long?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (warehouseId == null)
        {
            logger.LogWarning("No warehouse exists — seeded products will have no Stock rows.");
        }

        var now = timeProvider.GetUtcNow();
        var newProducts = 0;
        foreach (var seedProduct in seed.Products)
        {
            if (existingSlugs.Contains(seedProduct.Slug))
            {
                continue;
            }

            var product = new Product
            {
                Name = seedProduct.Name,
                Slug = seedProduct.Slug,
                NormalizedName = seedProduct.Name.ToUpperInvariant(),
                Sku = seedProduct.Sku,
                ShortDescription = seedProduct.ShortDescription,
                Description = seedProduct.Description,
                Specification = seedProduct.Specification,
                Price = seedProduct.Price,
                OldPrice = seedProduct.OldPrice,
                IsPublished = true,
                PublishedOn = now,
                IsVisibleIndividually = true,
                IsAllowToOrder = true,
                IsFeatured = seedProduct.IsFeatured,
                StockTrackingIsEnabled = true,
                StockQuantity = seedProduct.Stock,
                CreatedById = ownerId,
                CreatedOn = now,
                LatestUpdatedById = ownerId,
                LatestUpdatedOn = now
            };

            foreach (var categorySlug in seedProduct.Categories)
            {
                if (!categoriesBySlug.TryGetValue(categorySlug, out var category))
                {
                    throw new InvalidOperationException(
                        $"Seed product '{seedProduct.Slug}' references unknown category '{categorySlug}'.");
                }

                product.ProductCategories.Add(new ProductCategory { Category = category });
            }

            var displayOrder = 0;
            foreach (var image in seedProduct.Images)
            {
                var medium = new Medium { FileName = image, MediaType = MediaTypes.Image, FileSize = 0 };
                product.ProductMedia.Add(new ProductMedium { Media = medium, DisplayOrder = displayOrder++ });
                product.ThumbnailImage ??= medium;
            }

            if (warehouseId != null)
            {
                product.Stocks.Add(new Stock
                {
                    WarehouseId = warehouseId.Value,
                    Quantity = seedProduct.Stock,
                    ReservedQuantity = 0
                });
            }

            db.Products.Add(product);
            newProducts++;

            if (newProducts % 200 == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Catalog seed done: {NewCategories} new categories, {NewProducts} new products ({Skipped} already present).",
            newCategories, newProducts, seed.Products.Count - newProducts);
    }

    private sealed record CatalogSeed
    {
        public List<SeedCategory> Categories { get; init; } = [];
        public List<SeedProduct> Products { get; init; } = [];
    }

    private sealed record SeedCategory
    {
        public required string Slug { get; init; }
        public required string Name { get; init; }
        public string? Parent { get; init; }
        public int DisplayOrder { get; init; }
    }

    private sealed record SeedProduct
    {
        public required string Slug { get; init; }
        public required string Name { get; init; }
        public string? Sku { get; init; }
        public decimal Price { get; init; }
        public decimal? OldPrice { get; init; }
        public string? ShortDescription { get; init; }
        public string? Description { get; init; }
        public string? Specification { get; init; }
        public int Stock { get; init; }
        public List<string> Categories { get; init; } = [];
        public List<string> Images { get; init; } = [];
        public bool IsFeatured { get; init; }
    }
}
