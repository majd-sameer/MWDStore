using Microsoft.AspNetCore.Mvc;
using Store.Api.Controllers.Admin;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the admin write path for the Product Option and Product Attribute bilingual <c>Name</c>
/// (mirrors <see cref="ProductLocalizationTests"/>'s coverage of the same shared pattern): the
/// English name is stored atomically with the Arabic base <c>Name</c> column (same entity row, same
/// <c>LocalizedString</c>), the GET/List round-trip it back, and the list's <c>HasEnglish</c> flag
/// reflects whether <c>Name.En</c> is set.
/// </summary>
public class ProductOptionAttributeLocalizationTests
{
    private static AdminProductOptionsController NewOptionsController(StoreDbContext db) => new(db);

    private static AdminProductAttributesController NewAttributesController(StoreDbContext db) => new(db);

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

    // ===== Product options ==========================================================================

    [Fact]
    public async Task Option_create_persists_the_english_name()
    {
        using var db = TestDb.New();
        var controller = NewOptionsController(db);

        var created = FromCreated(await controller.Create(
            new ProductOptionUpsertRequest { Name = "اللون", NameEn = "Color" }, default));

        var entity = db.ProductOptions.Single(o => o.Id == created.Id);
        Assert.Equal("اللون", entity.Name.Ar);
        Assert.Equal("Color", entity.Name.En);
        Assert.Equal("Color", created.NameEn);
        Assert.True(created.HasEnglish);
        Assert.Equal("اللون", created.Name);
    }

    [Fact]
    public async Task Option_create_with_no_english_name_leaves_english_null()
    {
        using var db = TestDb.New();
        var controller = NewOptionsController(db);

        var created = FromCreated(await controller.Create(
            new ProductOptionUpsertRequest { Name = "المقاس", NameEn = null }, default));

        Assert.Null(db.ProductOptions.Single(o => o.Id == created.Id).Name.En);
        Assert.False(created.HasEnglish);
        Assert.Null(created.NameEn);
    }

    [Fact]
    public async Task Option_update_modifies_the_english_name_in_place_and_can_clear_it()
    {
        using var db = TestDb.New();
        var controller = NewOptionsController(db);
        var created = FromCreated(await controller.Create(
            new ProductOptionUpsertRequest { Name = "اللون", NameEn = "Color" }, default));

        var updated = FromOk(await controller.Update(
            created.Id, new ProductOptionUpsertRequest { Name = "اللون", NameEn = "Colour" }, default));
        Assert.Equal("Colour", updated.NameEn);
        Assert.Equal("Colour", db.ProductOptions.Single(o => o.Id == created.Id).Name.En);

        var cleared = FromOk(await controller.Update(
            created.Id, new ProductOptionUpsertRequest { Name = "اللون", NameEn = null }, default));
        Assert.Null(cleared.NameEn);
        Assert.False(cleared.HasEnglish);

        var fetched = FromOk(await controller.Get(created.Id, default));
        Assert.Null(fetched.NameEn);
    }

    [Fact]
    public async Task Option_list_reports_HasEnglish_per_row()
    {
        using var db = TestDb.New();
        var controller = NewOptionsController(db);
        var withEnglish = FromCreated(await controller.Create(
            new ProductOptionUpsertRequest { Name = "اللون", NameEn = "Color" }, default));
        var withoutEnglish = FromCreated(await controller.Create(
            new ProductOptionUpsertRequest { Name = "المقاس", NameEn = null }, default));

        var list = FromOk(await controller.List(default));

        Assert.True(list.Single(o => o.Id == withEnglish.Id).HasEnglish);
        Assert.False(list.Single(o => o.Id == withoutEnglish.Id).HasEnglish);
    }

    // ===== Product attributes ========================================================================

    private static async Task<long> SeedGroupAsync(StoreDbContext db)
    {
        var group = new ProductAttributeGroup { Name = "عام" };
        db.Set<ProductAttributeGroup>().Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    [Fact]
    public async Task Attribute_create_persists_the_english_name()
    {
        using var db = TestDb.New();
        var groupId = await SeedGroupAsync(db);
        var controller = NewAttributesController(db);

        var created = FromCreated(await controller.Create(
            new ProductAttributeUpsertRequest { Name = "المادة", GroupId = groupId, NameEn = "Material" }, default));

        var entity = db.ProductAttributes.Single(a => a.Id == created.Id);
        Assert.Equal("المادة", entity.Name.Ar);
        Assert.Equal("Material", entity.Name.En);
        Assert.Equal("Material", created.NameEn);
        Assert.True(created.HasEnglish);
    }

    [Fact]
    public async Task Attribute_create_with_no_english_name_leaves_english_null()
    {
        using var db = TestDb.New();
        var groupId = await SeedGroupAsync(db);
        var controller = NewAttributesController(db);

        var created = FromCreated(await controller.Create(
            new ProductAttributeUpsertRequest { Name = "اللياقة", GroupId = groupId, NameEn = null }, default));

        Assert.Null(db.ProductAttributes.Single(a => a.Id == created.Id).Name.En);
        Assert.False(created.HasEnglish);
    }

    [Fact]
    public async Task Attribute_update_modifies_the_english_name_in_place()
    {
        using var db = TestDb.New();
        var groupId = await SeedGroupAsync(db);
        var controller = NewAttributesController(db);
        var created = FromCreated(await controller.Create(
            new ProductAttributeUpsertRequest { Name = "المادة", GroupId = groupId, NameEn = "Material" }, default));

        var updated = FromOk(await controller.Update(
            created.Id, new ProductAttributeUpsertRequest { Name = "المادة", GroupId = groupId, NameEn = "Fabric" },
            default));

        Assert.Equal("Fabric", updated.NameEn);
        Assert.Equal("Fabric", db.ProductAttributes.Single(a => a.Id == created.Id).Name.En);
    }

    [Fact]
    public async Task Attribute_list_reports_HasEnglish_per_row()
    {
        using var db = TestDb.New();
        var groupId = await SeedGroupAsync(db);
        var controller = NewAttributesController(db);
        var withEnglish = FromCreated(await controller.Create(
            new ProductAttributeUpsertRequest { Name = "المادة", GroupId = groupId, NameEn = "Material" }, default));
        var withoutEnglish = FromCreated(await controller.Create(
            new ProductAttributeUpsertRequest { Name = "اللياقة", GroupId = groupId, NameEn = null }, default));

        var list = FromOk(await controller.List(default));

        Assert.True(list.Single(a => a.Id == withEnglish.Id).HasEnglish);
        Assert.False(list.Single(a => a.Id == withoutEnglish.Id).HasEnglish);
    }
}
