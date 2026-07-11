using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin category management (CRUD). Deletes are soft. Category names/descriptions/SEO
/// fields are bilingual <see cref="LocalizedString"/> values (Arabic in the base column, English in
/// the sibling "...En" column).</summary>
[ApiController]
[RequirePermission(Permissions.CatalogManage)]
[Route("api/admin/categories")]
public sealed class AdminCategoriesController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminCategoriesController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryDto>>> List(
        [FromQuery] bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var categories = _db.Categories.AsQueryable();
        if (!includeDeleted)
        {
            categories = categories.Where(c => !c.IsDeleted);
        }

        var rows = await categories.ToListAsync(cancellationToken);

        return Ok(rows
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name.Ar)
            .Select(ToDto)
            .ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminCategoryDto>> Get(long id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        return Ok(ToDto(category));
    }

    [HttpPost]
    public async Task<ActionResult<AdminCategoryDto>> Create(
        CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = new Category();
        Apply(category, request);

        if (await _db.Categories.AnyAsync(c => c.Slug == category.Slug, cancellationToken))
        {
            return Conflict(new { error = $"A category with slug '{category.Slug}' already exists." });
        }

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = category.Id }, ToDto(category));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminCategoryDto>> Update(
        long id, CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        if (request.ParentId == id)
        {
            return BadRequest(new { error = "A category cannot be its own parent." });
        }

        Apply(category, request);

        if (await _db.Categories.AnyAsync(c => c.Slug == category.Slug && c.Id != id, cancellationToken))
        {
            return Conflict(new { error = $"A category with slug '{category.Slug}' already exists." });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(category));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        if (await _db.Categories.AnyAsync(c => c.ParentId == id && !c.IsDeleted, cancellationToken))
        {
            return BadRequest(new { error = "Cannot delete a category that still has child categories." });
        }

        category.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(Category category, CategoryUpsertRequest request)
    {
        category.Name = new LocalizedString(request.Name, request.NameEn);
        category.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        category.Description = LocalizedString.From(request.Description, request.DescriptionEn);
        category.MetaTitle = LocalizedString.From(request.MetaTitle, request.MetaTitleEn);
        category.MetaKeywords = LocalizedString.From(request.MetaKeywords, request.MetaKeywordsEn);
        category.MetaDescription = LocalizedString.From(request.MetaDescription, request.MetaDescriptionEn);
        category.DisplayOrder = request.DisplayOrder;
        category.IsPublished = request.IsPublished;
        category.IncludeInMenu = request.IncludeInMenu;
        category.ParentId = request.ParentId;
    }

    private static AdminCategoryDto ToDto(Category c) => new(
        c.Id, c.Name.Ar!, c.Slug, c.Description?.Ar, c.DisplayOrder, c.IsPublished, c.IncludeInMenu, c.ParentId, c.IsDeleted,
        c.Name.En, c.Description?.En, c.MetaTitle?.En, c.MetaKeywords?.En, c.MetaDescription?.En,
        HasEnglish: c.Name.En != null || c.Description?.En != null || c.MetaTitle?.En != null
            || c.MetaKeywords?.En != null || c.MetaDescription?.En != null);
}
