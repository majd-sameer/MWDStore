using Microsoft.Extensions.DependencyInjection;
using Store.Api.Infrastructure;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>Covers <see cref="ContentSeeder"/>: inserts the fixed homepage block set (with an
/// English overlay) on a fresh database, and is a strict no-op — including leaving admin edits
/// untouched — the second time it runs.</summary>
public class ContentSeederTests
{
    private static ServiceProvider BuildProvider(StoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_creates_the_fixed_block_set_with_english_overlay()
    {
        var db = TestDb.New();
        var provider = BuildProvider(db);

        await ContentSeeder.SeedAsync(provider);

        var keys = db.ContentBlocks.Select(b => b.Key).ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                "home.hero", "home.story",
                "home.value.1", "home.value.2", "home.value.3", "home.value.4", "home.value.5",
                "home.cta",
            },
            keys);

        var hero = db.ContentBlocks.Single(b => b.Key == "home.hero");
        Assert.False(string.IsNullOrEmpty(hero.Title));
        Assert.True(hero.IsPublished);

        var heroTitleEn = db.LocalizedContentProperties.SingleOrDefault(
            p => p.EntityType == "ContentBlock" && p.EntityId == hero.Id
                && p.CultureId == "en-US" && p.ProperyName == "Title");
        Assert.NotNull(heroTitleEn);
        Assert.False(string.IsNullOrEmpty(heroTitleEn!.Value));
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_and_does_not_touch_existing_blocks()
    {
        var db = TestDb.New();
        var provider = BuildProvider(db);

        await ContentSeeder.SeedAsync(provider);
        var countAfterFirstRun = db.ContentBlocks.Count();

        // Simulate an admin edit before the next boot.
        var hero = db.ContentBlocks.Single(b => b.Key == "home.hero");
        hero.Title = "عنوان معدّل من الإدارة";
        db.SaveChanges();

        await ContentSeeder.SeedAsync(provider);

        Assert.Equal(countAfterFirstRun, db.ContentBlocks.Count());
        Assert.Equal("عنوان معدّل من الإدارة", db.ContentBlocks.Single(b => b.Key == "home.hero").Title);
    }
}
