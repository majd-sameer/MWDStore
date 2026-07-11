using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers;
using Store.Api.Controllers.Admin;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Content;
using Store.Application.Localization;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>
/// Covers the CMS Page bilingual <see cref="Store.Domain.LocalizedString"/> fields end to end
/// (mirrors <see cref="ProductLocalizationTests"/>'s coverage of the same shared pattern): the
/// admin write path persists Name/Body/SEO as LocalizedString, the admin GET/List read the English
/// side back, and the public <see cref="ContentController.Page"/> endpoint resolves per request
/// culture (falling back to the Arabic base when no/other translation exists).
/// </summary>
public class PageLocalizationTests
{
    private const long UserId = 1;

    private static AdminPagesController NewController(StoreDbContext db)
    {
        var controller = new AdminPagesController(db, TimeProvider.System);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "TestAuth")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static ContentController NewContentController(StoreDbContext db, IRequestCulture culture)
    {
        var controller = new ContentController(
            db, TimeProvider.System, new Store.Application.Common.LocalMediaUrlBuilder(),
            culture, new FakeContentBlockService());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    /// <summary>Content blocks are out of scope for this feature — a stub is enough to satisfy the
    /// controller's constructor.</summary>
    private sealed class FakeContentBlockService : IContentBlockService
    {
        public Task<IReadOnlyList<ContentBlockDto>> GetPublishedAsync(
            string? prefix, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentBlockDto>>([]);

        public Task<IReadOnlyList<AdminContentBlockDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminContentBlockDto>>([]);

        public Task<AdminContentBlockDto?> GetAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminContentBlockDto?>(null);

        public Task<AdminContentBlockDto?> UpdateAsync(
            long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminContentBlockDto?>(null);
    }

    private static PageUpsertRequest NewRequest(
        string name = "About Us", string? slug = "about-us", string? nameEn = "About Us EN",
        string? bodyEn = "Body EN", string? metaTitleEn = "Meta title EN",
        string? metaKeywordsEn = "meta,keywords,en", string? metaDescriptionEn = "Meta description EN") => new()
    {
        Name = name,
        Slug = slug,
        Body = "المحتوى بالعربية",
        MetaTitle = "عنوان الميتا",
        MetaKeywords = "كلمات",
        MetaDescription = "وصف الميتا",
        IsPublished = true,
        NameEn = nameEn,
        BodyEn = bodyEn,
        MetaTitleEn = metaTitleEn,
        MetaKeywordsEn = metaKeywordsEn,
        MetaDescriptionEn = metaDescriptionEn,
    };

    private static T FromOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static T FromCreated<T>(ActionResult<T> result)
    {
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        return Assert.IsType<T>(created.Value);
    }

    // ---- create -------------------------------------------------------------------------------

    [Fact]
    public async Task Create_persists_english_values_for_every_localized_field()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.Create(NewRequest(), default));

        Assert.Equal("About Us EN", created.NameEn);
        Assert.Equal("Body EN", created.BodyEn);
        Assert.Equal("Meta title EN", created.MetaTitleEn);
        Assert.Equal("meta,keywords,en", created.MetaKeywordsEn);
        Assert.Equal("Meta description EN", created.MetaDescriptionEn);
    }

    [Fact]
    public async Task Create_with_no_english_values_leaves_english_fields_null()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var created = FromCreated(await controller.Create(
            NewRequest(nameEn: null, bodyEn: null, metaTitleEn: null, metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        Assert.Null(created.NameEn);
        Assert.Null(created.BodyEn);
        Assert.False(created.HasEnglish);
    }

    // ---- detail GET -----------------------------------------------------------------------------

    [Fact]
    public async Task Get_returns_english_values_and_leaves_the_arabic_base_untouched()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var fetched = FromOk(await controller.Get(created.Id, default));

        Assert.Equal("About Us EN", fetched.NameEn);
        Assert.Equal("Body EN", fetched.BodyEn);
        Assert.Equal("Meta title EN", fetched.MetaTitleEn);
        Assert.Equal("meta,keywords,en", fetched.MetaKeywordsEn);
        Assert.Equal("Meta description EN", fetched.MetaDescriptionEn);

        Assert.Equal("About Us", fetched.Name);
        Assert.Equal("المحتوى بالعربية", fetched.Body);
    }

    // ---- update ---------------------------------------------------------------------------------

    [Fact]
    public async Task Update_modifies_the_existing_english_values_in_place()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var updated = FromOk(await controller.Update(
            created.Id, NewRequest(nameEn: "About Us EN v2", bodyEn: "Body EN v2"), default));

        Assert.Equal("About Us EN v2", updated.NameEn);
        Assert.Equal("Body EN v2", updated.BodyEn);
    }

    [Fact]
    public async Task Update_can_clear_an_existing_english_translation()
    {
        using var db = TestDb.New();
        var controller = NewController(db);
        var created = FromCreated(await controller.Create(NewRequest(), default));

        var updated = FromOk(await controller.Update(created.Id, NewRequest(nameEn: null), default));

        Assert.Null(updated.NameEn);
        Assert.Equal("Body EN", updated.BodyEn);

        var fetched = FromOk(await controller.Get(created.Id, default));
        Assert.Null(fetched.NameEn);
    }

    // ---- list: HasEnglish -------------------------------------------------------------------------

    [Fact]
    public async Task List_flags_HasEnglish_true_only_when_an_english_property_exists()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var withEnglish = FromCreated(await controller.Create(
            NewRequest(name: "Has English", slug: "has-english"), default));
        var withoutEnglish = FromCreated(await controller.Create(
            NewRequest(name: "No English", slug: "no-english", nameEn: null, bodyEn: null,
                metaTitleEn: null, metaKeywordsEn: null, metaDescriptionEn: null),
            default));

        var list = FromOk(await controller.List(default));

        Assert.True(list.Single(p => p.Id == withEnglish.Id).HasEnglish);
        Assert.False(list.Single(p => p.Id == withoutEnglish.Id).HasEnglish);
    }

    // ---- public read ----------------------------------------------------------------------------

    [Fact]
    public async Task PublicPage_serves_english_values_under_english_culture()
    {
        using var db = TestDb.New();
        var admin = NewController(db);
        await admin.Create(NewRequest(), default);

        var content = NewContentController(db, TestCulture.English);
        var page = FromOk(await content.Page("about-us", default));

        Assert.Equal("About Us EN", page.Name);
        Assert.Equal("Body EN", page.Body);
        Assert.Equal("Meta title EN", page.MetaTitle);
    }

    [Fact]
    public async Task PublicPage_serves_arabic_base_under_arabic_culture()
    {
        using var db = TestDb.New();
        var admin = NewController(db);
        await admin.Create(NewRequest(), default);

        var content = NewContentController(db, TestCulture.Arabic);
        var page = FromOk(await content.Page("about-us", default));

        Assert.Equal("About Us", page.Name);
        Assert.Equal("المحتوى بالعربية", page.Body);
    }

    [Fact]
    public async Task PublicPage_falls_back_to_arabic_base_when_english_translation_is_missing()
    {
        using var db = TestDb.New();
        var admin = NewController(db);
        await admin.Create(
            NewRequest(name: "No English", slug: "no-english", nameEn: null, bodyEn: null,
                metaTitleEn: null, metaKeywordsEn: null, metaDescriptionEn: null),
            default);

        var content = NewContentController(db, TestCulture.English);
        var page = FromOk(await content.Page("no-english", default));

        Assert.Equal("No English", page.Name);
        Assert.Equal("المحتوى بالعربية", page.Body);
    }

    [Fact]
    public async Task PublicPage_returns_not_found_for_unknown_or_unpublished_slug()
    {
        using var db = TestDb.New();
        var content = NewContentController(db, TestCulture.English);

        var result = await content.Page("does-not-exist", default);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
