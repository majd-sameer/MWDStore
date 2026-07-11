using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers.Admin;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the admin write path for the Product English text now that it lives in the product's own
/// <see cref="LocalizedString"/> columns (Arabic base + sibling "...En"): create/update persist the
/// EN value atomically with the base columns, the detail GET reads it back, clearing EN round-trips
/// to null, and the list's <c>HasEnglish</c> flag reflects whether an English <c>Name</c> is present.
/// </summary>
public class ProductLocalizationTests
{
    private const long UserId = 1;

    /// <summary>Trivial media storage stand-in — these tests never upload files, only reference ids.</summary>
    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(originalFileName);

        public void Delete(string? fileName)
        {
        }

        public string? GetUrl(string? fileName) => fileName;
    }

    private static AdminProductsController NewController(StoreDbContext db)
    {
        var controller = new AdminProductsController(db, TimeProvider.System, new FakeMediaStorage());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "TestAuth")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static ProductUpsertRequest NewRequest(
        string name = "Widget", string? nameEn = "Widget EN", string? shortDescriptionEn = "Short EN",
        string? descriptionEn = "Description EN", string? metaTitleEn = "Meta title EN",
        string? metaKeywordsEn = "meta,keywords,en", string? metaDescriptionEn = "Meta description EN") => new()
    {
        Name = name,
        ShortDescription = "الوصف القصير",
        Description = "الوصف الكامل",
        Price = 19.99m,
        NameEn = nameEn,
        ShortDescriptionEn = shortDescriptionEn,
        DescriptionEn = descriptionEn,
        MetaTitleEn = metaTitleEn,
        MetaKeywordsEn = metaKeywordsEn,
        MetaDescriptionEn = metaDescriptionEn,
    };

    // IsAssignableFrom (not IsType) because the list endpoints declare an IReadOnlyList<T> return type
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
    public async Task Create_persists_english_columns_for_every_localized_field()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.Create(NewRequest(), default));

        var product = db.Products.Single(p => p.Id == created.Id);

        Assert.Equal("Widget EN", product.Name.En);
        Assert.Equal("Short EN", product.ShortDescription!.En);
        Assert.Equal("Description EN", product.Description!.En);
        Assert.Equal("Meta title EN", product.MetaTitle!.En);
        Assert.Equal("meta,keywords,en", product.MetaKeywords!.En);
        Assert.Equal("Meta description EN", product.MetaDescription!.En);

        // Base (Arabic) columns hold the request's base values.
        Assert.Equal("Widget", product.Name.Ar);
        Assert.Equal("الوصف القصير", product.ShortDescription.Ar);
    }

    [Fact]
    public async Task Create_with_no_english_values_leaves_english_columns_null()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.Create(
            NewRequest(nameEn: null, shortDescriptionEn: null, descriptionEn: null, metaTitleEn: null,
                metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        var product = db.Products.Single(p => p.Id == created.Id);

        Assert.Null(product.Name.En);
        Assert.Null(product.ShortDescription!.En);
        Assert.Null(product.Description!.En);
    }

    // ---- detail GET -----------------------------------------------------------------------------

    [Fact]
    public async Task Detail_get_returns_english_values()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var fetched = FromOk(await controller.Get(created.Id, default));

        Assert.Equal("Widget EN", fetched.NameEn);
        Assert.Equal("Short EN", fetched.ShortDescriptionEn);
        Assert.Equal("Description EN", fetched.DescriptionEn);
        Assert.Equal("Meta title EN", fetched.MetaTitleEn);
        Assert.Equal("meta,keywords,en", fetched.MetaKeywordsEn);
        Assert.Equal("Meta description EN", fetched.MetaDescriptionEn);

        // Arabic base columns are untouched.
        Assert.Equal("Widget", fetched.Name);
        Assert.Equal("الوصف القصير", fetched.ShortDescription);
    }

    [Fact]
    public async Task Detail_get_returns_null_english_fields_when_never_set()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(
            NewRequest(nameEn: null, shortDescriptionEn: null, descriptionEn: null, metaTitleEn: null,
                metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        var fetched = FromOk(await controller.Get(created.Id, default));

        Assert.Null(fetched.NameEn);
        Assert.Null(fetched.ShortDescriptionEn);
        Assert.Null(fetched.DescriptionEn);
    }

    // ---- update ---------------------------------------------------------------------------------

    [Fact]
    public async Task Update_modifies_the_existing_english_columns_in_place()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var updated = FromOk(await controller.Update(
            created.Id, NewRequest(nameEn: "Widget EN v2", descriptionEn: "Description EN v2"), default));

        Assert.Equal("Widget EN v2", updated.NameEn);
        Assert.Equal("Description EN v2", updated.DescriptionEn);

        // Still a single product row — update mutates in place, it does not duplicate.
        var product = db.Products.Single(p => p.Id == created.Id);
        Assert.Equal("Widget EN v2", product.Name.En);
    }

    [Fact]
    public async Task Update_can_clear_an_existing_english_translation()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var updated = FromOk(await controller.Update(
            created.Id, NewRequest(nameEn: null), default));

        Assert.Null(updated.NameEn);
        // The other EN fields, left populated in the request, are unaffected.
        Assert.Equal("Short EN", updated.ShortDescriptionEn);

        // Round-trips through a fresh Get too (not just the returned DTO).
        var fetched = FromOk(await controller.Get(created.Id, default));
        Assert.Null(fetched.NameEn);
    }

    [Fact]
    public async Task Update_can_add_an_english_translation_that_was_never_set_on_create()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(
            NewRequest(nameEn: null, shortDescriptionEn: null, descriptionEn: null, metaTitleEn: null,
                metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        var updated = FromOk(await controller.Update(created.Id, NewRequest(nameEn: "Now in English"), default));

        Assert.Equal("Now in English", updated.NameEn);
    }

    // ---- list: HasEnglish -------------------------------------------------------------------------

    [Fact]
    public async Task List_flags_HasEnglish_true_only_when_the_name_english_column_exists()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var withEnglish = FromCreated(await controller.Create(NewRequest(name: "Has English"), default));
        var withoutEnglish = FromCreated(await controller.Create(
            NewRequest(name: "No English", nameEn: null, shortDescriptionEn: null, descriptionEn: null,
                metaTitleEn: null, metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        var list = FromOk(await controller.List(null, cancellationToken: default));

        Assert.True(list.Single(p => p.Id == withEnglish.Id).HasEnglish);
        Assert.False(list.Single(p => p.Id == withoutEnglish.Id).HasEnglish);
    }

    [Fact]
    public async Task List_HasEnglish_turns_false_after_clearing_the_english_name()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        await controller.Update(created.Id, NewRequest(nameEn: null), default);

        var list = FromOk(await controller.List(null, cancellationToken: default));

        Assert.False(list.Single(p => p.Id == created.Id).HasEnglish);
    }
}
