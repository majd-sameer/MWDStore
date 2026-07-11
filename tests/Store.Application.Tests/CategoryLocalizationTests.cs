using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers.Admin;
using Store.Api.Models;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>
/// Covers the admin category CRUD's bilingual <see cref="Store.Domain.LocalizedString"/> fields
/// (mirrors <c>ContentBlockServiceTests</c>' admin coverage): create/update persist the
/// <c>NameEn</c>/<c>DescriptionEn</c>/SEO-EN fields, a cleared translation round-trips, detail GET
/// surfaces the English fields, and <c>HasEnglish</c> reflects whether any translation exists.
/// </summary>
public class CategoryLocalizationTests
{
    private static AdminCategoriesController NewController(StoreDbContext db) => new(db);

    private static T Body<T>(ActionResult<T> result)
    {
        if (result.Result is OkObjectResult ok)
        {
            return Assert.IsType<T>(ok.Value);
        }
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        return Assert.IsType<T>(created.Value);
    }

    private static IReadOnlyList<T> ListBody<T>(ActionResult<IReadOnlyList<T>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<List<T>>(ok.Value);
    }

    private static CategoryUpsertRequest BaseRequest(string name = "أدوات المطبخ") => new()
    {
        Name = name,
        Description = "وصف عربي",
        DisplayOrder = 1,
        IsPublished = true,
        IncludeInMenu = true,
    };

    [Fact]
    public async Task Create_PersistsEnglishOverlay()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var request = BaseRequest();
        request.NameEn = "Kitchen Tools";
        request.DescriptionEn = "English description";
        request.MetaTitleEn = "Kitchen Tools | Store";
        request.MetaKeywordsEn = "kitchen, tools";
        request.MetaDescriptionEn = "Shop kitchen tools";

        var created = Body(await controller.Create(request, default));

        Assert.Equal("أدوات المطبخ", created.Name);
        Assert.Equal("Kitchen Tools", created.NameEn);
        Assert.Equal("English description", created.DescriptionEn);
        Assert.Equal("Kitchen Tools | Store", created.MetaTitleEn);
        Assert.Equal("kitchen, tools", created.MetaKeywordsEn);
        Assert.Equal("Shop kitchen tools", created.MetaDescriptionEn);
        Assert.True(created.HasEnglish);

        // Round-trips through a fresh Get (not just the returned DTO).
        var fetched = Body(await controller.Get(created.Id, default));
        Assert.Equal("Kitchen Tools", fetched.NameEn);
        Assert.True(fetched.HasEnglish);
    }

    [Fact]
    public async Task Update_PersistsEnglishOverlay()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = Body(await controller.Create(BaseRequest(), default));
        Assert.False(created.HasEnglish);

        var update = BaseRequest();
        update.NameEn = "Updated EN Name";
        update.DescriptionEn = "Updated EN description";

        var updated = Body(await controller.Update(created.Id, update, default));

        Assert.Equal("Updated EN Name", updated.NameEn);
        Assert.Equal("Updated EN description", updated.DescriptionEn);
        Assert.True(updated.HasEnglish);
    }

    [Fact]
    public async Task Update_CanClearAnExistingEnglishTranslation()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var withEnglish = BaseRequest();
        withEnglish.NameEn = "Has EN";
        withEnglish.DescriptionEn = "Has EN description";
        var created = Body(await controller.Create(withEnglish, default));
        Assert.True(created.HasEnglish);

        var clearing = BaseRequest();
        clearing.NameEn = null;
        clearing.DescriptionEn = null;

        var cleared = Body(await controller.Update(created.Id, clearing, default));

        Assert.Null(cleared.NameEn);
        Assert.Null(cleared.DescriptionEn);
        Assert.False(cleared.HasEnglish);

        // Round-trips through a fresh Get too.
        var fetched = Body(await controller.Get(created.Id, default));
        Assert.Null(fetched.NameEn);
        Assert.False(fetched.HasEnglish);
    }

    [Fact]
    public async Task HasEnglish_IsFalse_WhenNoTranslationExists()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = Body(await controller.Create(BaseRequest(), default));

        Assert.False(created.HasEnglish);
        Assert.Null(created.NameEn);
    }

    [Fact]
    public async Task List_ReportsHasEnglish_PerRow()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var translated = BaseRequest("مترجم");
        translated.NameEn = "Translated";
        await controller.Create(translated, default);

        await controller.Create(BaseRequest("غير مترجم"), default);

        var list = ListBody(await controller.List(false, default));

        var translatedRow = Assert.Single(list, c => c.Name == "مترجم");
        var untranslatedRow = Assert.Single(list, c => c.Name == "غير مترجم");
        Assert.True(translatedRow.HasEnglish);
        Assert.False(untranslatedRow.HasEnglish);
    }
}
