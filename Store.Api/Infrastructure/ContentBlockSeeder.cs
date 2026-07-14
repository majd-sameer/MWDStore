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
        // Home
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

        // About (/pages/about-us) — every visible line of copy, fixed design
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

        // Story video — editable heading + a YouTube watch/share URL. The storefront turns the link
        // into an embed and hides the whole section when an admin clears it.
        new("about", "about-video", "eyebrow", "text", "شاهد قصتنا", "Watch our story", Sort: 0),
        new("about", "about-video", "title", "text",
            "من داخل المراكز… حكاية إرادة", "From inside the centers… a story of will", Sort: 1),
        new("about", "about-video", "body", "text",
            "يأخذكم هذا الفيلم القصير إلى داخل مراكز الإصلاح والتأهيل في الأردن، حيث يتعلّم النزلاء حرفةً يتقنونها ويحوّلونها إلى قطع يدوية تُباع عبر هذا المتجر الرسمي. خلف كل منتج إنسانٌ اختار أن يبدأ من جديد — وكل عملية شراء تدعم تأهيله ورحلته للعودة إلى المجتمع بكرامة وأمل.",
            "This short film takes you inside Jordan's Reform & Rehabilitation Centers, where residents "
            + "learn a craft, master it, and turn it into handmade pieces sold through this official "
            + "store. Behind every product is a person choosing to start over — and every purchase "
            + "supports their rehabilitation and their journey back into society with dignity and hope.",
            Sort: 2),
        new("about", "about-video", "youtube", "link", null, null,
            LinkUrl: "https://www.youtube.com/watch?v=DLS0Ic3wrj0&t=31s", Sort: 3),

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

        // Footer (global) — editable copy + social links, fixed design
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

        // Follow-us column — heading over the social links + location.
        new("footer", "footer-follow-us", "heading", "text", "تابعنا", "Follow us", Sort: 0),

        // Social links — icon is code-owned (by BlockKey); admins set the URL and toggle visibility.
        // Seeded with an empty URL so nothing shows until configured in the Site Content editor.
        new("footer", "footer-social", "facebook", "link", null, null, Sort: 0),
        new("footer", "footer-social", "instagram", "link", null, null, Sort: 1),
        new("footer", "footer-social", "twitter", "link", null, null, Sort: 2),
        new("footer", "footer-social", "youtube", "link", null, null, Sort: 3),
        new("footer", "footer-social", "tiktok", "link", null, null, Sort: 4),
        new("footer", "footer-social", "whatsapp", "link", null, null, Sort: 5),

        // Location — editable address line plus an optional map link (empty until an admin sets it).
        new("footer", "footer-location", "address", "text",
            "عمّان، الأردن · مديرية الأمن العام — إدارة مراكز الإصلاح والتأهيل",
            "Amman, Jordan · Public Security Directorate — Reform & Rehabilitation Centers Administration",
            Sort: 0),
        new("footer", "footer-location", "map", "link", null, null, Sort: 1),

        // FAQ (/pages/faq) — editable heading + Q&A pairs, fixed design
        new("faq", "faq-hero", "title", "text",
            "الأسئلة الشائعة", "Frequently asked questions", Sort: 0),
        new("faq", "faq-hero", "subtitle", "text",
            "إجابات سريعة حول منتجاتنا وطلباتك والمبادرة التي تقف خلفها.",
            "Quick answers about our products, your orders, and the initiative behind them.", Sort: 1),

        new("faq", "faq-list", "q1", "text", "ما هي مبادرة “صُنع بعزيمة”؟", "What is Made With Determination?", Sort: 0),
        new("faq", "faq-list", "a1", "text",
            "مبادرة وطنية من مديرية الأمن العام تتيح لنزلاء مراكز الإصلاح والتأهيل في الأردن بيع منتجاتهم المصنوعة يدويًا — وكل عملية شراء تدعم تأهيلهم وإعادة دمجهم.",
            "A national initiative by the Public Security Directorate that lets residents of Jordan's Reform "
            + "& Rehabilitation Centers sell the crafts they make by hand — every purchase supports their "
            + "rehabilitation and reintegration.", Sort: 1),
        new("faq", "faq-list", "q2", "text", "من يصنع المنتجات؟", "Who makes the products?", Sort: 2),
        new("faq", "faq-list", "a2", "text",
            "يصنع كل قطعة نزلاء المراكز يدويًا، بعد تدريبهم على يد مدرّبين متخصصين، من خامات مختارة بعناية.",
            "Residents of the centers, trained by specialized instructors, craft every piece by hand from "
            + "carefully selected materials.", Sort: 3),
        new("faq", "faq-list", "q3", "text", "هل عمليات الدفع آمنة؟", "Are my payments secure?", Sort: 4),
        new("faq", "faq-list", "a3", "text",
            "نعم. هذه منصّة حكومية رسمية، وتتم جميع عمليات الدفع عبر اتصال آمن ومشفّر.",
            "Yes. This is an official government platform and all payments are processed over a secure, "
            + "encrypted connection.", Sort: 5),
        new("faq", "faq-list", "q4", "text", "كم تستغرق مدة التوصيل؟", "How long does delivery take?", Sort: 6),
        new("faq", "faq-list", "a4", "text",
            "تُسلَّم الطلبات عادةً خلال ٣–٥ أيام عمل داخل الأردن. ويمكنك متابعة طلبك في أي وقت من خلال “تتبّع طلب”.",
            "Orders are usually delivered within 3–5 business days across Jordan. You can follow your order "
            + "any time from “Track an order”.", Sort: 7),
        new("faq", "faq-list", "q5", "text", "هل يمكنني إرجاع منتج أو استبداله؟", "Can I return or exchange an item?", Sort: 8),
        new("faq", "faq-list", "a5", "text",
            "يمكن إرجاع أو استبدال المنتجات غير المستخدمة وبحالتها الأصلية خلال ١٤ يومًا من الاستلام. راجع صفحة التوصيل والإرجاع للتفاصيل.",
            "Unused items in their original condition can be returned or exchanged within 14 days of "
            + "delivery. See Delivery & returns for details.", Sort: 9),
        new("faq", "faq-list", "q6", "text", "أين تذهب العائدات؟", "Where do the proceeds go?", Sort: 10),
        new("faq", "faq-list", "a6", "text",
            "١٠٠٪ من العائدات تذهب لتأهيل الصنّاع ومساعدتهم على الاندماج في المجتمع بكرامة.",
            "100% of proceeds go toward rehabilitating the makers and helping them reintegrate into society "
            + "with dignity.", Sort: 11),
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

        await SeederSupport.EnsureCultureAsync(db, RequestCulture.EnglishCultureId, cancellationToken);

        var blocks = await db.ContentBlocks
            .Select(b => new { b.Id, b.PageKey, b.SectionKey, b.BlockKey })
            .ToListAsync(cancellationToken);
        var blockIds = blocks.Select(b => b.Id).ToList();
        var blockIdsByKey = new Dictionary<(string PageKey, string SectionKey, string BlockKey), long>();
        foreach (var block in blocks)
        {
            blockIdsByKey.TryAdd((block.PageKey, block.SectionKey, block.BlockKey), block.Id);
        }

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
            if (!blockIdsByKey.TryGetValue((seed.Page, seed.Section, seed.Key), out var blockId)
                || overlaid.Contains(blockId))
            {
                continue;
            }

            db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = LocalizedEntity.ContentBlock,
                EntityId = blockId,
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
