using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the initial inventory of editable content blocks for the storefront (home + about pages).
/// Insert-by-key only, so re-runs never duplicate a block and never overwrite an admin edit (Arabic in
/// the block's <c>Value</c>, English as a <c>LocalizedContentProperty</c> overlay — the same mechanism
/// product and news translations use). Additive and idempotent, matching the other seeders.
/// </summary>
public static class ContentBlockSeeder
{
    private sealed record BlockSeed(
        string Page, string Section, string Key, string Type,
        string? Ar, string? En, string? LinkUrl = null, int Sort = 0);

    private static readonly BlockSeed[] Blocks =
    [
        // ----- Home ---------------------------------------------------------------------------------
        new("home", "hero-grid", "hero-copy.title", "text",
            "منتجات صُنعت بعزيمة وأيادٍ تستحق الفرصة",
            "Products made with determination by hands that deserve a chance", Sort: 0),
        new("home", "hero-grid", "hero-copy.subtitle", "text",
            "قطع يدوية فريدة يصنعها نزلاء مراكز الإصلاح والتأهيل. كل عملية شراء تدعم تأهيلهم وتعيد لهم الكرامة والأمل.",
            "Unique handmade pieces crafted by inmates of the Reform & Rehabilitation Centers. "
            + "Every purchase supports their rehabilitation and restores dignity and hope.", Sort: 1),
        new("home", "hero-grid", "hero-copy.cta-label", "text", "تسوّق الآن", "Shop now", Sort: 2),
        new("home", "hero-grid", "hero-copy.cta", "link", null, null, LinkUrl: "/shop", Sort: 3),
        new("home", "hero-grid", "hero-media", "image", null, null, Sort: 4),
        new("home", "mission-band", "mission.title", "text",
            "وراء كل قطعة… إنسانٌ يستعيد كرامته", "Behind every piece… a person reclaiming their dignity"),
        new("home", "cta-band", "cta.title", "text",
            "كن أول من يعرف عن المنتجات الجديدة", "Be the first to know about new products"),

        // ----- About (/pages/about-us) — every visible line of copy, fixed design -------------------
        new("about", "about-hero", "eyebrow", "text", "من نحن", "About us", Sort: 0),
        new("about", "about-hero", "title", "text",
            "صُنع بعزيمة — حين تتحوّل الإرادة إلى منتج",
            "Made with Determination — when willpower becomes a product", Sort: 1),
        new("about", "about-hero", "body", "text",
            "مبادرة وطنية من مديرية الأمن العام / إدارة مراكز الإصلاح والتأهيل، تتيح لنزلاء المراكز عرض منتجاتهم اليدوية وبيعها رقميًا، فتتحوّل مهاراتهم إلى مصدر رزق كريم وجسرٍ لإعادة الاندماج في المجتمع.",
            "A national initiative by the Public Security Directorate / Correction and Rehabilitation "
            + "Centers Department that enables center residents to showcase and sell their handmade "
            + "products online — turning their skills into a dignified livelihood and a bridge back into "
            + "society.", Sort: 2),
        new("about", "about-hero", "cta-label", "text", "تصفّح المنتجات", "Browse products", Sort: 3),

        new("about", "about-how", "eyebrow", "text", "كيف نعمل", "How we work", Sort: 0),
        new("about", "about-how", "title", "text",
            "من المهارة إلى يديك في أربع خطوات", "From skill to your hands in four steps", Sort: 1),
        new("about", "about-how", "step1.number", "text", "١", "1", Sort: 2),
        new("about", "about-how", "step1.title", "text", "تدريب وتأهيل", "Training & rehabilitation", Sort: 3),
        new("about", "about-how", "step1.text", "text",
            "يتعلّم النزلاء حِرَفًا يدوية على يد مدرّبين متخصصين.",
            "Residents learn handcrafts from specialized trainers.", Sort: 4),
        new("about", "about-how", "step2.number", "text", "٢", "2", Sort: 5),
        new("about", "about-how", "step2.title", "text", "صناعة بإتقان", "Crafted with care", Sort: 6),
        new("about", "about-how", "step2.text", "text",
            "تُصنع كل قطعة يدويًا من خامات طبيعية مختارة.",
            "Every piece is handmade from carefully selected natural materials.", Sort: 7),
        new("about", "about-how", "step3.number", "text", "٣", "3", Sort: 8),
        new("about", "about-how", "step3.title", "text", "فحص الجودة", "Quality inspection", Sort: 9),
        new("about", "about-how", "step3.text", "text",
            "تخضع المنتجات لمعايير دقيقة قبل عرضها للبيع.",
            "Products are held to strict standards before going on sale.", Sort: 10),
        new("about", "about-how", "step4.number", "text", "٤", "4", Sort: 11),
        new("about", "about-how", "step4.title", "text", "دعم وإعادة دمج", "Support & reintegration", Sort: 12),
        new("about", "about-how", "step4.text", "text",
            "يُوجَّه العائد لتمكين الصانع وإعادة دمجه.",
            "Proceeds go toward empowering makers and reintegrating them.", Sort: 13),

        new("about", "about-values", "eyebrow", "text", "قيمنا", "Our values", Sort: 0),
        new("about", "about-values", "title", "text", "ما الذي نؤمن به", "What we believe in", Sort: 1),
        new("about", "about-values", "trust.title", "text", "الثقة", "Trust", Sort: 2),
        new("about", "about-values", "trust.text", "text",
            "منصّة حكومية رسمية تضمن أصالة كل قطعة ووضوح مصدرها.",
            "An official government platform that guarantees the authenticity and provenance of every piece.",
            Sort: 3),
        new("about", "about-values", "empower.title", "text", "التمكين", "Empowerment", Sort: 4),
        new("about", "about-values", "empower.text", "text",
            "عائدات البيع تعود مباشرة لتأهيل النزلاء وإعادة دمجهم.",
            "Sales proceeds go directly to rehabilitating residents and reintegrating them.", Sort: 5),
        new("about", "about-values", "heritage.title", "text", "التراث", "Heritage", Sort: 6),
        new("about", "about-values", "heritage.text", "text",
            "حِرَف أردنية أصيلة تُصنع يدويًا وتحافظ على هويتنا.",
            "Authentic Jordanian crafts, handmade to preserve our identity.", Sort: 7),
        new("about", "about-values", "dignity.title", "text", "الكرامة", "Dignity", Sort: 8),
        new("about", "about-values", "dignity.text", "text",
            "عمل شريف يعيد بناء الإنسان ويمنحه فرصة جديدة.",
            "Honest work that rebuilds a person and offers a new beginning.", Sort: 9),
        new("about", "about-values", "quality.title", "text", "الجودة", "Quality", Sort: 10),
        new("about", "about-values", "quality.text", "text",
            "معايير دقيقة لكل منتج قبل أن يصل إلى يديك.",
            "Strict standards for every product before it reaches your hands.", Sort: 11),

        // ----- Footer (global) — editable copy + social links, fixed design --------------------------
        new("footer", "footer-brand", "tagline", "text",
            "منتجات يدوية من صنع نزلاء مراكز الإصلاح والتأهيل في الأردن — ١٠٠٪ من العائدات تدعم التأهيل وإعادة الدمج.",
            "Handmade products by inmates of Jordan's Reform & Rehabilitation Centers — 100% of proceeds "
            + "support rehabilitation and reintegration.", Sort: 0),
        new("footer", "footer-brand", "psd", "text",
            "بإشراف مديرية الأمن العام — إدارة مراكز الإصلاح والتأهيل",
            "Under the supervision of the Public Security Directorate — Reform & Rehabilitation Centers "
            + "Administration", Sort: 1),
        new("footer", "footer-brand", "newsletter", "text",
            "بريدك لنشرة الأسبوع", "Email for the weekly drop", Sort: 2),

        new("footer", "footer-shop", "heading", "text", "المتجر", "Shop", Sort: 0),

        new("footer", "footer-company", "heading", "text", "صُنع بعزيمة", "Made With Determination", Sort: 0),
        new("footer", "footer-company", "about", "text", "قصّتنا", "Our story", Sort: 1),
        new("footer", "footer-company", "makers", "text", "صنّاعنا", "Our makers", Sort: 2),
        new("footer", "footer-company", "stores", "text", "المتاجر", "Stores", Sort: 3),

        new("footer", "footer-care", "heading", "text", "العناية", "Care", Sort: 0),
        new("footer", "footer-care", "delivery_returns", "text", "التوصيل والإرجاع", "Delivery & returns", Sort: 1),
        new("footer", "footer-care", "track", "text", "تتبّع طلبًا", "Track an order", Sort: 2),
        new("footer", "footer-care", "contact", "text", "تواصل معنا", "Contact", Sort: 3),
        new("footer", "footer-care", "faq", "text", "الأسئلة الشائعة", "FAQ", Sort: 4),

        // Social links — icon is code-owned (by BlockKey); admins set the URL and toggle visibility.
        // Seeded with an empty URL so nothing shows until configured in the Site Content editor.
        new("footer", "footer-social", "facebook", "link", null, null, Sort: 0),
        new("footer", "footer-social", "instagram", "link", null, null, Sort: 1),
        new("footer", "footer-social", "twitter", "link", null, null, Sort: 2),
        new("footer", "footer-social", "youtube", "link", null, null, Sort: 3),
        new("footer", "footer-social", "tiktok", "link", null, null, Sort: 4),
        new("footer", "footer-social", "whatsapp", "link", null, null, Sort: 5),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ContentBlockSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();

        var existing = (await db.ContentBlocks
                .Select(b => new { b.PageKey, b.SectionKey, b.BlockKey })
                .ToListAsync(cancellationToken))
            .Select(k => (k.PageKey, k.SectionKey, k.BlockKey))
            .ToHashSet();

        var inserted = 0;
        foreach (var seed in Blocks)
        {
            if (existing.Contains((seed.Page, seed.Section, seed.Key)))
            {
                continue;
            }

            db.ContentBlocks.Add(new ContentBlock
            {
                PageKey = seed.Page,
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
            .Select(b => new { b.Id, b.PageKey, b.SectionKey, b.BlockKey })
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
            var block = blocks.FirstOrDefault(
                b => b.PageKey == seed.Page && b.SectionKey == seed.Section && b.BlockKey == seed.Key);
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
