using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Loads content overrides from <c>translations.&lt;culture&gt;.json</c> into the
/// <c>LocalizedContentProperty</c> table. English (<c>translations.en.json</c> → <c>en-US</c>) carries the
/// full catalog; Arabic (<c>translations.ar.json</c> → <c>arabic</c>) carries only the rows whose base
/// columns still contain English, so the Arabic storefront reads as fully Arabic. Missing rows fall
/// back to the base column.
///
/// Strictly additive + idempotent: it upserts (inserts new rows, updates changed values, leaves
/// matching rows untouched), so it is safe to re-run on every boot and after regenerating a file.
/// File shape: <c>{ "Product": { "&lt;id&gt;": { "Name": "...", "Description": "..." } }, "NewsItem": {...} }</c>.
/// </summary>
public static class LocalizationSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly (string File, string CultureId)[] Sources =
    [
        ("translations.en.json", RequestCulture.EnglishCultureId),
        ("translations.ar.json", RequestCulture.ArabicCultureId),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("LocalizationSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();
        var contentRoot = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        foreach (var (file, cultureId) in Sources)
        {
            await LoadAsync(db, Path.Combine(contentRoot, file), cultureId, logger, cancellationToken);
        }
    }

    private static async Task LoadAsync(
        StoreDbContext db, string path, string cultureId, ILogger logger, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            logger.LogInformation("{File} not found — skipping {Culture} localization seeding.", Path.GetFileName(path), cultureId);
            return;
        }

        Dictionary<string, Dictionary<string, Dictionary<string, string?>>>? file;
        await using (var stream = File.OpenRead(path))
        {
            file = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, Dictionary<string, string?>>>>(
                stream, JsonOptions, cancellationToken);
        }

        if (file is null || file.Count == 0)
        {
            logger.LogWarning("{File} is empty — nothing to seed.", Path.GetFileName(path));
            return;
        }

        if (!await db.Cultures.AnyAsync(c => c.Id == cultureId, cancellationToken))
        {
            db.Cultures.Add(new Culture { Id = cultureId, Name = cultureId });
            await db.SaveChangesAsync(cancellationToken);
        }

        var entityTypes = file.Keys.ToList();
        var existing = await db.LocalizedContentProperties
            .Where(p => p.CultureId == cultureId && entityTypes.Contains(p.EntityType!))
            .ToListAsync(cancellationToken);
        var index = existing.ToDictionary(p => (p.EntityType, p.EntityId, p.ProperyName));

        int inserted = 0, updated = 0;
        foreach (var (entityType, rows) in file)
        {
            foreach (var (idText, properties) in rows)
            {
                if (!long.TryParse(idText, out var entityId))
                {
                    continue;
                }

                foreach (var (property, value) in properties)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (index.TryGetValue((entityType, entityId, property), out var row))
                    {
                        if (row.Value != value)
                        {
                            row.Value = value;
                            updated++;
                        }
                    }
                    else
                    {
                        db.LocalizedContentProperties.Add(new LocalizedContentProperty
                        {
                            EntityType = entityType,
                            EntityId = entityId,
                            CultureId = cultureId,
                            ProperyName = property,
                            Value = value,
                        });
                        inserted++;
                    }
                }
            }
        }

        if (inserted > 0 || updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Localization seed ({Culture}): {Inserted} new, {Updated} updated.", cultureId, inserted, updated);
    }
}
