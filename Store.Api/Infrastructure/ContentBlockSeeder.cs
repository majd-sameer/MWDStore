using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the initial inventory of editable content blocks for the home page. Insert-by-key only, so
/// re-runs never duplicate a block and never overwrite an admin edit (Arabic in the block's
/// <c>Value</c>, English as a <c>LocalizedContentProperty</c> overlay — the same mechanism product and
/// news translations use). Additive and idempotent, matching the other seeders.
/// </summary>
public static class ContentBlockSeeder
{
    private const string Page = "home";

    private sealed record BlockSeed(
        string Section, string Key, string Type, string? Ar, string? En, string? LinkUrl = null, int Sort = 0);

    private static readonly BlockSeed[] Blocks =
    [
        new("hero-grid", "hero-copy.title", "text",
            "منتجات صُنعت بعزيمة وأيادٍ تستحق الفرصة",
            "Products made with determination by hands that deserve a chance", Sort: 0),
        new("hero-grid", "hero-copy.subtitle", "text",
            "قطع يدوية فريدة يصنعها نزلاء مراكز الإصلاح والتأهيل. كل عملية شراء تدعم تأهيلهم وتعيد لهم الكرامة والأمل.",
            "Unique handmade pieces crafted by inmates of the Reform & Rehabilitation Centers. "
            + "Every purchase supports their rehabilitation and restores dignity and hope.", Sort: 1),
        new("hero-grid", "hero-copy.cta-label", "text", "تسوّق الآن", "Shop now", Sort: 2),
        new("hero-grid", "hero-copy.cta", "link", null, null, LinkUrl: "/shop", Sort: 3),
        new("hero-grid", "hero-media", "image", null, null, Sort: 4),
        new("mission-band", "mission.title", "text",
            "وراء كل قطعة… إنسانٌ يستعيد كرامته", "Behind every piece… a person reclaiming their dignity"),
        new("cta-band", "cta.title", "text",
            "كن أول من يعرف عن المنتجات الجديدة", "Be the first to know about new products"),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ContentBlockSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();

        var existing = (await db.ContentBlocks
                .Where(b => b.PageKey == Page)
                .Select(b => new { b.SectionKey, b.BlockKey })
                .ToListAsync(cancellationToken))
            .Select(k => (k.SectionKey, k.BlockKey))
            .ToHashSet();

        var inserted = 0;
        foreach (var seed in Blocks)
        {
            if (existing.Contains((seed.Section, seed.Key)))
            {
                continue;
            }

            db.ContentBlocks.Add(new ContentBlock
            {
                PageKey = Page,
                SectionKey = seed.Section,
                BlockKey = seed.Key,
                Type = seed.Type,
                Value = seed.Ar,
                LinkUrl = seed.LinkUrl,
                IsActive = true,
                SortOrder = seed.Sort,
                CreatedOn = now,
                UpdatedOn = now,
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Cultures.AnyAsync(c => c.Id == RequestCulture.EnglishCultureId, cancellationToken))
        {
            db.Cultures.Add(new Culture
            {
                Id = RequestCulture.EnglishCultureId,
                Name = RequestCulture.EnglishCultureId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var blocks = await db.ContentBlocks
            .Where(b => b.PageKey == Page)
            .Select(b => new { b.Id, b.SectionKey, b.BlockKey })
            .ToListAsync(cancellationToken);
        var blockIds = blocks.Select(b => b.Id).ToList();

        var overlaid = (await db.LocalizedContentProperties
                .Where(p => p.EntityType == LocalizedEntity.ContentBlock
                    && p.CultureId == RequestCulture.EnglishCultureId
                    && p.ProperyName == LocalizedProperty.Value
                    && blockIds.Contains(p.EntityId))
                .Select(p => p.EntityId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var overlayInserted = 0;
        foreach (var seed in Blocks.Where(s => !string.IsNullOrEmpty(s.En)))
        {
            var block = blocks.FirstOrDefault(b => b.SectionKey == seed.Section && b.BlockKey == seed.Key);
            if (block is null || overlaid.Contains(block.Id))
            {
                continue;
            }

            db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = LocalizedEntity.ContentBlock,
                EntityId = block.Id,
                CultureId = RequestCulture.EnglishCultureId,
                ProperyName = LocalizedProperty.Value,
                Value = seed.En,
            });
            overlayInserted++;
        }

        if (overlayInserted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Content block seed: {Inserted} new blocks, {Overlay} English overlays.", inserted, overlayInserted);
    }
}
