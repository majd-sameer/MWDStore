using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers.Admin;
using Store.Api.Models;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>
/// Covers the admin brand CRUD's bilingual <see cref="Store.Domain.LocalizedString"/> fields
/// (mirrors <see cref="CategoryLocalizationTests"/>): create/update persist <c>NameEn</c>/
/// <c>DescriptionEn</c>, a cleared translation round-trips, detail GET surfaces the English fields,
/// and <c>HasEnglish</c> reflects whether any translation exists.
/// </summary>
public class BrandLocalizationTests
{
    private static AdminBrandsController NewController(StoreDbContext db) => new(db);

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

    private static BrandUpsertRequest BaseRequest(string name = "علامة تجارية") => new()
    {
        Name = name,
        Description = "وصف عربي",
        IsPublished = true,
    };

    [Fact]
    public async Task Create_PersistsEnglishOverlay()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var request = BaseRequest();
        request.NameEn = "Brand Name";
        request.DescriptionEn = "English description";

        var created = Body(await controller.Create(request, default));

        Assert.Equal("علامة تجارية", created.Name);
        Assert.Equal("Brand Name", created.NameEn);
        Assert.Equal("English description", created.DescriptionEn);
        Assert.True(created.HasEnglish);

        var fetched = Body(await controller.Get(created.Id, default));
        Assert.Equal("Brand Name", fetched.NameEn);
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

        var translated = BaseRequest("مترجمة");
        translated.NameEn = "Translated";
        await controller.Create(translated, default);

        await controller.Create(BaseRequest("غير مترجمة"), default);

        var list = ListBody(await controller.List(false, default));

        var translatedRow = Assert.Single(list, b => b.Name == "مترجمة");
        var untranslatedRow = Assert.Single(list, b => b.Name == "غير مترجمة");
        Assert.True(translatedRow.HasEnglish);
        Assert.False(untranslatedRow.HasEnglish);
    }
}
