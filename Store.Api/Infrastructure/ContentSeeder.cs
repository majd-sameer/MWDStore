using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the fixed set of homepage <c>ContentBlock</c> rows (hero, mission/story, the five "our
/// values" cards, CTA band) with today's hardcoded copy — so switching the storefront sections over
/// to reading from the API changes nothing visible. Idempotent by <see cref="ContentBlock.Key"/>:
/// a key that already exists is left untouched (an admin may have edited it since), only missing
/// keys are inserted. Title/Text/LinkText are written directly as bilingual
/// <see cref="LocalizedString"/> values.
/// </summary>
public static class ContentSeeder
{
    private sealed record Seed(
        string Key, int SortOrder,
        string? TitleAr, string? TextAr, string? ImageUrl, string? LinkUrl, string? LinkTextAr,
        string? TitleEn, string? TextEn, string? LinkTextEn);

    private static readonly Seed[] Seeds =
    [
        new Seed(
            "home.hero", 1,
            "منتجات صُنعت بعزيمة وأيادٍ تستحق الفرصة",
            "قطع يدوية فريدة يصنعها نزلاء مراكز الإصلاح والتأهيل. كل عملية شراء تدعم تأهيلهم وتعيد لهم الكرامة والأمل.",
            "/home-hero.jpg", "/shop", "تسوّق الآن",
            "Products made with determination by hands that deserve a chance",
            "Unique handmade pieces crafted by inmates of the Reform & Rehabilitation Centers. Every purchase supports their rehabilitation and restores dignity and hope.",
            "Shop now"),

        new Seed(
            "home.story", 2,
            "وراء كل قطعة… إنسانٌ يستعيد كرامته",
            "«صُنع بعزيمة» مبادرة من مديرية الأمن العام تُتيح لنزلاء مراكز الإصلاح والتأهيل عرض منتجاتهم اليدوية وبيعها عبر الإنترنت، لتحويل المهارة إلى مصدر رزق كريم ودعمٍ لإعادة الدمج في المجتمع.",
            null, "/pages/about-us", "اقرأ المزيد",
            "Behind every piece… a person reclaiming their dignity",
            "“Made with Determination” is a Public Security Directorate initiative that lets inmates of the Reform & Rehabilitation Centers showcase and sell their handmade work online — turning skill into a dignified livelihood and a path back into society.",
            "Read more"),

        new Seed(
            "home.value.1", 10,
            "الثقة", "منصّة حكومية رسمية تضمن أصالة كل قطعة ووضوح مصدرها.", null, null, null,
            "Trust", "An official government platform that guarantees the authenticity and provenance of every piece.", null),

        new Seed(
            "home.value.2", 11,
            "التمكين", "عائدات البيع تعود مباشرة لتأهيل النزلاء وإعادة دمجهم.", null, null, null,
            "Empowerment", "Sales proceeds go directly to rehabilitating residents and reintegrating them.", null),

        new Seed(
            "home.value.3", 12,
            "التراث", "حِرَف أردنية أصيلة تُصنع يدويًا وتحافظ على هويتنا.", null, null, null,
            "Heritage", "Authentic Jordanian crafts, handmade to preserve our identity.", null),

        new Seed(
            "home.value.4", 13,
            "الكرامة", "عمل شريف يعيد بناء الإنسان ويمنحه فرصة جديدة.", null, null, null,
            "Dignity", "Honest work that rebuilds a person and offers a new beginning.", null),

        new Seed(
            "home.value.5", 14,
            "الجودة", "معايير دقيقة لكل منتج قبل أن يصل إلى يديك.", null, null, null,
            "Quality", "Strict standards for every product before it reaches your hands.", null),

        new Seed(
            "home.cta", 20,
            "كن أول من يعرف عن المنتجات الجديدة",
            "اشترك ليصلك جديد الحِرَف وقصص النجاح وعروض حصرية تدعم رسالتنا.",
            null, null, null,
            "Be the first to know about new products",
            "Subscribe for new crafts, success stories and exclusive offers that support our mission.",
            null),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ContentSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();

        var existingKeys = (await db.ContentBlocks.Select(b => b.Key).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = Seeds.Where(s => !existingKeys.Contains(s.Key)).ToList();
        if (toInsert.Count == 0)
        {
            logger.LogInformation("Content blocks already seeded — nothing to do.");
            return;
        }

        foreach (var seed in toInsert)
        {
            var block = new ContentBlock
            {
                Key = seed.Key,
                Title = new LocalizedString(seed.TitleAr, seed.TitleEn),
                Text = LocalizedString.From(seed.TextAr, seed.TextEn),
                ImageUrl = seed.ImageUrl,
                LinkUrl = seed.LinkUrl,
                LinkText = LocalizedString.From(seed.LinkTextAr, seed.LinkTextEn),
                SortOrder = seed.SortOrder,
                IsPublished = true,
            };
            db.ContentBlocks.Add(block);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Content blocks seeded: {Count} new.", toInsert.Count);
    }
}
