using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers.Admin;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>
/// Covers the admin write path for the NewsItem bilingual <see cref="Store.Domain.LocalizedString"/>
/// fields — the same shared pattern as <see cref="ProductLocalizationTests"/> and
/// <see cref="ContentBlockServiceTests"/>: create/update persist Name/ShortContent/FullContent as
/// LocalizedString, the detail GET reads the English side back, and the list's <c>HasEnglish</c>
/// flag reflects whether an EN <c>Name</c> exists.
/// </summary>
public class NewsLocalizationTests
{
    private const long UserId = 1;

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(originalFileName);

        public void Delete(string? fileName)
        {
        }

        public string? GetUrl(string? fileName) => fileName;
    }

    private static AdminNewsController NewController(StoreDbContext db)
    {
        var controller = new AdminNewsController(db, TimeProvider.System, new FakeMediaStorage());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "TestAuth")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static NewsItemUpsertRequest NewRequest(
        string name = "Breaking Story", string? nameEn = "Breaking Story EN",
        string? shortContentEn = "Short EN", string? fullContentEn = "Full EN") => new()
    {
        Name = name,
        ShortContent = "المحتوى القصير",
        FullContent = "المحتوى الكامل",
        NameEn = nameEn,
        ShortContentEn = shortContentEn,
        FullContentEn = fullContentEn,
    };

    // IsAssignableFrom (not IsType) because the list endpoint declares an IReadOnlyList<T> return type
    // while the runtime value is a concrete List<T>.
    private static T FromOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static T FromCreated<T>(ActionResult<T> result)
    {
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        return Assert.IsAssignableFrom<T>(created.Value);
    }

    // ---- create -------------------------------------------------------------------------------

    [Fact]
    public async Task CreateItem_persists_english_values_for_every_localized_field()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.CreateItem(NewRequest(), default));

        Assert.Equal("Breaking Story EN", created.NameEn);
        Assert.Equal("Short EN", created.ShortContentEn);
        Assert.Equal("Full EN", created.FullContentEn);
    }

    [Fact]
    public async Task CreateItem_with_no_english_values_leaves_english_fields_null()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.CreateItem(
            NewRequest(nameEn: null, shortContentEn: null, fullContentEn: null), default));

        Assert.Null(created.NameEn);
        Assert.Null(created.ShortContentEn);
        Assert.Null(created.FullContentEn);
    }

    // ---- detail GET -----------------------------------------------------------------------------

    [Fact]
    public async Task GetItem_returns_english_values()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(NewRequest(), default));

        var fetched = FromOk(await controller.GetItem(created.Id, default));

        Assert.Equal("Breaking Story EN", fetched.NameEn);
        Assert.Equal("Short EN", fetched.ShortContentEn);
        Assert.Equal("Full EN", fetched.FullContentEn);

        // Arabic base columns are untouched.
        Assert.Equal("Breaking Story", fetched.Name);
        Assert.Equal("المحتوى القصير", fetched.ShortContent);
    }

    [Fact]
    public async Task GetItem_returns_null_english_fields_when_never_set()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(
            NewRequest(nameEn: null, shortContentEn: null, fullContentEn: null), default));

        var fetched = FromOk(await controller.GetItem(created.Id, default));

        Assert.Null(fetched.NameEn);
        Assert.Null(fetched.ShortContentEn);
        Assert.Null(fetched.FullContentEn);
    }

    // ---- update ---------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateItem_modifies_the_existing_english_values_in_place()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(NewRequest(), default));

        var updated = FromOk(await controller.UpdateItem(
            created.Id, NewRequest(nameEn: "Breaking Story EN v2", fullContentEn: "Full EN v2"), default));

        Assert.Equal("Breaking Story EN v2", updated.NameEn);
        Assert.Equal("Full EN v2", updated.FullContentEn);
    }

    [Fact]
    public async Task UpdateItem_can_clear_an_existing_english_translation()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(NewRequest(), default));

        var updated = FromOk(await controller.UpdateItem(created.Id, NewRequest(nameEn: null), default));

        Assert.Null(updated.NameEn);
        Assert.Equal("Short EN", updated.ShortContentEn);

        var fetched = FromOk(await controller.GetItem(created.Id, default));
        Assert.Null(fetched.NameEn);
    }

    [Fact]
    public async Task UpdateItem_can_add_an_english_translation_that_was_never_set_on_create()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(
            NewRequest(nameEn: null, shortContentEn: null, fullContentEn: null), default));

        var updated = FromOk(await controller.UpdateItem(created.Id, NewRequest(nameEn: "Now in English"), default));

        Assert.Equal("Now in English", updated.NameEn);
    }

    // ---- list: HasEnglish -------------------------------------------------------------------------

    [Fact]
    public async Task ListItems_flags_HasEnglish_true_only_when_the_name_has_english()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var withEnglish = FromCreated(await controller.CreateItem(NewRequest(name: "Has English"), default));
        var withoutEnglish = FromCreated(await controller.CreateItem(
            NewRequest(name: "No English", nameEn: null, shortContentEn: null, fullContentEn: null), default));

        var list = FromOk(await controller.ListItems(default));

        Assert.True(list.Single(n => n.Id == withEnglish.Id).HasEnglish);
        Assert.False(list.Single(n => n.Id == withoutEnglish.Id).HasEnglish);
    }

    [Fact]
    public async Task ListItems_HasEnglish_turns_false_after_clearing_the_english_name()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.CreateItem(NewRequest(), default));

        await controller.UpdateItem(created.Id, NewRequest(nameEn: null), default);

        var list = FromOk(await controller.ListItems(default));

        Assert.False(list.Single(n => n.Id == created.Id).HasEnglish);
    }
}
