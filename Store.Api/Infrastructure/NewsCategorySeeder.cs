using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// The three fixed, code-known news-category slugs. Components and queries reference these slugs;
/// <see cref="NewsCategorySeeder"/> guarantees the rows exist. Never hard-code the numeric ids.
/// </summary>
public static class NewsCategorySlugs
{
    public const string SuccessStory = "success-story";
    public const string Activity = "activity";
    public const string Alert = "alert";
}

/// <summary>
/// Seeds the three purpose-built news categories (success story, activity, alert) by slug. Insert-by-slug
/// only, so re-runs never duplicate a category and never overwrite an admin rename (Arabic in the base
/// <c>Name</c> column, English as a <c>LocalizedContentProperty</c> overlay — the mechanism product and
/// news translations already use). Additive and idempotent, matching the other seeders.
/// </summary>
public static class NewsCategorySeeder
{
    private sealed record CategorySeed(string Slug, string Ar, string En, int DisplayOrder);

    private static readonly CategorySeed[] Categories =
    [
        new(NewsCategorySlugs.SuccessStory, "قصص نجاح", "Success Stories", 1),
        new(NewsCategorySlugs.Activity, "أنشطة", "Activities", 2),
        new(NewsCategorySlugs.Alert, "تنبيهات", "Alerts", 3),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("NewsCategorySeeder");
        var db = sp.GetRequiredService<StoreDbContext>();

        var existingSlugs = (await db.NewsCategories
                .Select(c => c.Slug)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var inserted = 0;
        foreach (var seed in Categories)
        {
            if (existingSlugs.Contains(seed.Slug))
            {
                continue;
            }

            db.NewsCategories.Add(new NewsCategory
            {
                Name = seed.Ar,
                Slug = seed.Slug,
                DisplayOrder = seed.DisplayOrder,
                IsPublished = true,
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeederSupport.EnsureCultureAsync(db, RequestCulture.EnglishCultureId, cancellationToken);

        var rows = await db.NewsCategories
            .Where(c => Categories.Select(s => s.Slug).Contains(c.Slug))
            .Select(c => new { c.Id, c.Slug })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();

        // Only seed the English overlay when it is missing, so an admin's English rename is never clobbered.
        var overlaid = (await db.LocalizedContentProperties
                .Where(p => p.EntityType == LocalizedEntity.NewsCategory
                    && p.CultureId == RequestCulture.EnglishCultureId
                    && p.ProperyName == LocalizedProperty.Name
                    && ids.Contains(p.EntityId))
                .Select(p => p.EntityId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var overlayInserted = 0;
        foreach (var seed in Categories)
        {
            var row = rows.FirstOrDefault(r => r.Slug == seed.Slug);
            if (row is null || overlaid.Contains(row.Id))
            {
                continue;
            }

            db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = LocalizedEntity.NewsCategory,
                EntityId = row.Id,
                CultureId = RequestCulture.EnglishCultureId,
                ProperyName = LocalizedProperty.Name,
                Value = seed.En,
            });
            overlayInserted++;
        }

        if (overlayInserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "News category seed: {Inserted} new categories, {Overlay} English overlays.", inserted, overlayInserted);
    }
}
