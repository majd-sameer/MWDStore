using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the Jordan location data the storefront and stock-tracking depend on (runtime port of
/// <c>Store.Migrator/11_seed_jordan.sql</c>):
///   - country <c>JO</c> (billing/shipping/city/zip enabled, districts off),
///   - the 12 governorates as <see cref="StateOrProvince"/> rows (ISO 3166-2:JO codes),
///   - a single "Main Warehouse" in Amman (<see cref="Address"/> + <see cref="Warehouse"/>) so
///     stock-tracked products are orderable.
/// Idempotent and additive: every insert is guarded by an existence check, so it is safe to run on
/// every startup and never modifies or deletes existing rows. Must run before <see cref="CatalogSeeder"/>
/// so seeded products can attach stock to the warehouse.
/// </summary>
public static class LocationSeeder
{
    private const string CountryId = "JO";
    private const string WarehouseName = "Main Warehouse";

    // ISO 3166-2:JO governorate codes + names, mirroring 11_seed_jordan.sql.
    private static readonly (string Code, string Name)[] Governorates =
    [
        ("AM", "Amman"),
        ("IR", "Irbid"),
        ("AZ", "Zarqa"),
        ("BA", "Al-Balqa"),
        ("MD", "Madaba"),
        ("MA", "Mafraq"),
        ("JA", "Jerash"),
        ("AJ", "Ajloun"),
        ("KA", "Karak"),
        ("AT", "Tafilah"),
        ("MN", "Ma'an"),
        ("AQ", "Aqaba"),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("LocationSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();

        if (!await db.Countries.AnyAsync(c => c.Id == CountryId, cancellationToken))
        {
            db.Countries.Add(new Country
            {
                Id = CountryId,
                Name = "Jordan",
                Code3 = "JOR",
                IsBillingEnabled = true,
                IsShippingEnabled = true,
                IsCityEnabled = true,
                IsZipCodeEnabled = true,
                IsDistrictEnabled = false
            });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded country [{CountryId}].", CountryId);
        }

        var existingCodes = (await db.StateOrProvinces
                .Where(s => s.CountryId == CountryId)
                .Select(s => s.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newGovernorates = 0;
        foreach (var (code, name) in Governorates)
        {
            if (existingCodes.Contains(code))
            {
                continue;
            }

            db.StateOrProvinces.Add(new StateOrProvince
            {
                CountryId = CountryId,
                Code = code,
                Name = name,
                Type = "Governorate"
            });
            newGovernorates++;
        }

        if (newGovernorates > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} governorate(s).", newGovernorates);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Name == WarehouseName, cancellationToken))
        {
            var ammanId = await db.StateOrProvinces
                .Where(s => s.CountryId == CountryId && s.Code == "AM")
                .Select(s => (long?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (ammanId == null)
            {
                logger.LogWarning("Amman governorate not found — skipping warehouse seeding.");
                return;
            }

            var address = new Address
            {
                ContactName = WarehouseName,
                AddressLine1 = "Amman",
                City = "Amman",
                ZipCode = "11118",
                CountryId = CountryId,
                StateOrProvinceId = ammanId.Value
            };
            db.Addresses.Add(address);
            await db.SaveChangesAsync(cancellationToken);

            db.Warehouses.Add(new Warehouse { Name = WarehouseName, AddressId = address.Id });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded '{Warehouse}' in Amman.", WarehouseName);
        }
    }
}
