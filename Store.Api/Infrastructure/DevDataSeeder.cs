using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the minimum reference + sample data a Development environment needs to exercise the full storefront
/// flow end-to-end (catalog → cart → checkout → order, plus warehouse inventory): one Country, one
/// StateOrProvince (required by <c>OrderAddress</c>), a Warehouse, a published/orderable sample product, and a
/// matching warehouse Stock row. Idempotent and Development-only — never wire this into production.
/// </summary>
public static class DevDataSeeder
{
    public const string SampleProductSlug = "newman-sample-product";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DevDataSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();
        var timeProvider = sp.GetRequiredService<TimeProvider>();

        // 1) Country.
        if (!await db.Countries.AnyAsync(c => c.Id == "US", cancellationToken))
        {
            db.Countries.Add(new Country
            {
                Id = "US",
                Name = "United States",
                Code3 = "USA",
                IsBillingEnabled = true,
                IsShippingEnabled = true,
                IsCityEnabled = true,
                IsZipCodeEnabled = true
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // 2) StateOrProvince (OrderAddress requires a valid StateOrProvinceId).
        var state = await db.StateOrProvinces.FirstOrDefaultAsync(s => s.CountryId == "US", cancellationToken);
        if (state == null)
        {
            state = new StateOrProvince { CountryId = "US", Code = "TS", Name = "Test State", Type = "State" };
            db.StateOrProvinces.Add(state);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3) Warehouse (+ its Address).
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(cancellationToken);
        if (warehouse == null)
        {
            var address = new Address
            {
                ContactName = "Main Warehouse",
                AddressLine1 = "1 Warehouse Way",
                City = "Testville",
                ZipCode = "12345",
                CountryId = "US",
                StateOrProvinceId = state.Id
            };
            db.Addresses.Add(address);
            await db.SaveChangesAsync(cancellationToken);

            warehouse = new Warehouse { Name = "Main Warehouse", AddressId = address.Id };
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 4) Catalog categories (published + in-menu) so the storefront has a navigable tree.
        var electronics = await EnsureCategoryAsync(db, "electronics", "Electronics", displayOrder: 1, parentId: null, cancellationToken);
        var laptops = await EnsureCategoryAsync(db, "laptops", "Laptops", displayOrder: 1, parentId: electronics.Id, cancellationToken);
        await EnsureCategoryAsync(db, "smartphones", "Smartphones", displayOrder: 2, parentId: electronics.Id, cancellationToken);
        await EnsureCategoryAsync(db, "home-kitchen", "Home & Kitchen", displayOrder: 2, parentId: null, cancellationToken);
        await EnsureCategoryAsync(db, "books", "Books", displayOrder: 3, parentId: null, cancellationToken);

        // 5) Sample product (published, orderable, stock-tracked) — needs an existing user for CreatedBy.
        var ownerId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        if (ownerId == 0)
        {
            logger.LogWarning("No user exists yet — skipping sample product seeding (run after IdentitySeeder).");
            return;
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == SampleProductSlug, cancellationToken);
        if (product == null)
        {
            var now = timeProvider.GetUtcNow();
            product = new Product
            {
                Name = new LocalizedString("Newman Sample Product"),
                Slug = SampleProductSlug,
                NormalizedName = "NEWMAN SAMPLE PRODUCT",
                ShortDescription = new LocalizedString("A sample product seeded for integration testing."),
                Price = 49.99m,
                IsPublished = true,
                PublishedOn = now,
                IsVisibleIndividually = true,
                IsAllowToOrder = true,
                StockTrackingIsEnabled = true,
                StockQuantity = 1000,
                CreatedById = ownerId,
                CreatedOn = now,
                LatestUpdatedById = ownerId,
                LatestUpdatedOn = now
            };
            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded sample product '{Slug}' (id {Id}).", SampleProductSlug, product.Id);
        }

        // 6) Link the sample product to a category so category listings aren't empty.
        if (!await db.Set<ProductCategory>().AnyAsync(pc => pc.ProductId == product.Id && pc.CategoryId == laptops.Id, cancellationToken))
        {
            db.Set<ProductCategory>().Add(new ProductCategory { ProductId = product.Id, CategoryId = laptops.Id });
            await db.SaveChangesAsync(cancellationToken);
        }

        // 7) Warehouse stock row for the sample product.
        if (!await db.Stocks.AnyAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id, cancellationToken))
        {
            db.Stocks.Add(new Stock
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 1000,
                ReservedQuantity = 0
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Dev data ready: country=US state={StateId} warehouse={WarehouseId} product={ProductId}.",
            state.Id, warehouse.Id, product.Id);
    }

    /// <summary>Gets (or creates) a published, in-menu category by slug. Idempotent.</summary>
    private static async Task<Category> EnsureCategoryAsync(
        StoreDbContext db, string slug, string name, int displayOrder, long? parentId, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
        if (category == null)
        {
            category = new Category
            {
                Name = new LocalizedString(name),
                Slug = slug,
                DisplayOrder = displayOrder,
                ParentId = parentId,
                IsPublished = true,
                IncludeInMenu = true,
                IsDeleted = false
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
        }

        return category;
    }
}
