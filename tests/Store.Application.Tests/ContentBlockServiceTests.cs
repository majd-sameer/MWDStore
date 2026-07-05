using Store.Application.Content;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the storefront read path (published-only, prefix filter, English overlay applied with
/// fallback to the Arabic base) and the admin write path (round-trips base + English overlay
/// fields, including clearing a translation) of <see cref="ContentBlockService"/>.
/// </summary>
public class ContentBlockServiceTests
{
    private static ContentBlockService NewService(StoreDbContext db) => new(db, new LocalizationService(db));

    private static StoreDbContext SeedFixture()
    {
        var db = TestDb.New();

        db.ContentBlocks.AddRange(
            new ContentBlock
            {
                Id = 1, Key = "home.hero", Title = "عنوان البطل", Text = "نص البطل",
                ImageUrl = "/home-hero.jpg", LinkUrl = "/shop", LinkText = "تسوّق الآن",
                SortOrder = 1, IsPublished = true,
            },
            new ContentBlock
            {
                Id = 2, Key = "home.story", Title = "عنوان القصة", Text = "نص القصة",
                SortOrder = 2, IsPublished = true,
            },
            new ContentBlock
            {
                Id = 3, Key = "home.value.1", Title = "الثقة", Text = "نص الثقة",
                SortOrder = 10, IsPublished = true,
            },
            new ContentBlock
            {
                Id = 4, Key = "other.banner", Title = "غير ذلك", Text = "نص آخر",
                SortOrder = 1, IsPublished = true,
            },
            new ContentBlock
            {
                Id = 5, Key = "home.draft", Title = "مسودة", Text = "غير منشور",
                SortOrder = 30, IsPublished = false,
            });

        db.Cultures.Add(new Culture { Id = "en-US", Name = "en-US" });
        db.LocalizedContentProperties.AddRange(
            new LocalizedContentProperty
            {
                EntityType = "ContentBlock", EntityId = 1, CultureId = "en-US",
                ProperyName = "Title", Value = "Hero title",
            },
            new LocalizedContentProperty
            {
                EntityType = "ContentBlock", EntityId = 1, CultureId = "en-US",
                ProperyName = "Text", Value = "Hero text",
            });

        db.SaveChanges();
        return db;
    }

    // ---- storefront reads ---------------------------------------------------

    [Fact]
    public async Task GetPublishedAsync_excludes_unpublished_blocks()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.GetPublishedAsync(prefix: null, cultureId: null);

        Assert.DoesNotContain(blocks, b => b.Key == "home.draft");
    }

    [Fact]
    public async Task GetPublishedAsync_filters_by_key_prefix()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.GetPublishedAsync(prefix: "home", cultureId: null);

        Assert.Equal(["home.hero", "home.story", "home.value.1"], blocks.Select(b => b.Key));
    }

    [Fact]
    public async Task GetPublishedAsync_orders_by_sort_order()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.GetPublishedAsync(prefix: "home", cultureId: null);

        Assert.Equal(["home.hero", "home.story", "home.value.1"], blocks.Select(b => b.Key));
    }

    [Fact]
    public async Task GetPublishedAsync_with_no_culture_returns_base_arabic_values()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.GetPublishedAsync(prefix: "home", cultureId: null);
        var hero = blocks.Single(b => b.Key == "home.hero");

        Assert.Equal("عنوان البطل", hero.Title);
        Assert.Equal("نص البطل", hero.Text);
    }

    [Fact]
    public async Task GetPublishedAsync_with_english_culture_applies_overlay()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.GetPublishedAsync(prefix: "home", cultureId: "en-US");
        var hero = blocks.Single(b => b.Key == "home.hero");

        Assert.Equal("Hero title", hero.Title);
        Assert.Equal("Hero text", hero.Text);
    }

    [Fact]
    public async Task GetPublishedAsync_falls_back_to_base_value_when_translation_missing()
    {
        var db = SeedFixture();
        var service = NewService(db);

        // "home.story" has no English overlay row.
        var blocks = await service.GetPublishedAsync(prefix: "home", cultureId: "en-US");
        var story = blocks.Single(b => b.Key == "home.story");

        Assert.Equal("عنوان القصة", story.Title);
        Assert.Equal("نص القصة", story.Text);
    }

    // ---- admin ----------------------------------------------------------------

    [Fact]
    public async Task ListAsync_includes_unpublished_blocks()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var blocks = await service.ListAsync();

        Assert.Contains(blocks, b => b.Key == "home.draft");
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        var db = SeedFixture();
        var service = NewService(db);

        Assert.Null(await service.GetAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_round_trips_base_and_english_fields()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var request = new ContentBlockUpdateRequest(
            Title: "عنوان محدث", Text: "نص محدث", ImageUrl: "/new-image.jpg",
            LinkUrl: "/new-link", LinkText: "رابط جديد", SortOrder: 5, IsPublished: false,
            TitleEn: "Updated title", TextEn: "Updated text", LinkTextEn: "New link");

        var updated = await service.UpdateAsync(2, request);

        Assert.NotNull(updated);
        Assert.Equal("عنوان محدث", updated!.Title);
        Assert.Equal("نص محدث", updated.Text);
        Assert.Equal("/new-image.jpg", updated.ImageUrl);
        Assert.Equal("/new-link", updated.LinkUrl);
        Assert.Equal("رابط جديد", updated.LinkText);
        Assert.Equal(5, updated.SortOrder);
        Assert.False(updated.IsPublished);
        Assert.Equal("Updated title", updated.TitleEn);
        Assert.Equal("Updated text", updated.TextEn);
        Assert.Equal("New link", updated.LinkTextEn);

        // Round-trips through a fresh GetAsync too (not just the returned DTO).
        var fetched = await service.GetAsync(2);
        Assert.Equal("Updated title", fetched!.TitleEn);

        // And the published storefront read now sees the new English overlay.
        var published = await service.GetPublishedAsync(prefix: "home", cultureId: "en-US");
        Assert.Null(published.SingleOrDefault(b => b.Key == "home.story")); // now unpublished
    }

    [Fact]
    public async Task UpdateAsync_can_clear_an_existing_english_translation()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var request = new ContentBlockUpdateRequest(
            Title: "عنوان البطل", Text: "نص البطل", ImageUrl: "/home-hero.jpg",
            LinkUrl: "/shop", LinkText: "تسوّق الآن", SortOrder: 1, IsPublished: true,
            TitleEn: null, TextEn: "Hero text", LinkTextEn: null);

        var updated = await service.UpdateAsync(1, request);

        Assert.Null(updated!.TitleEn);
        Assert.Equal("Hero text", updated.TextEn);

        // Falls back to the Arabic base on the public read once the English title is cleared.
        var published = await service.GetPublishedAsync(prefix: "home", cultureId: "en-US");
        var hero = published.Single(b => b.Key == "home.hero");
        Assert.Equal("عنوان البطل", hero.Title);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_unknown_id()
    {
        var db = SeedFixture();
        var service = NewService(db);

        var request = new ContentBlockUpdateRequest(
            "t", "x", null, null, null, 0, true, null, null, null);

        Assert.Null(await service.UpdateAsync(999, request));
    }
}
