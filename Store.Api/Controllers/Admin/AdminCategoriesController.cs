using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin category management (CRUD). Deletes are soft. <c>Name</c> and <c>Description</c> are
/// bilingual: Arabic in the base columns, English in the <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Catalog)]
[Route("api/admin/categories")]
public sealed class AdminCategoriesController : ControllerBase
{
    private const string EntityType = LocalizedEntity.Category;
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminCategoriesController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
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

        var items = await categories
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder,
                c.IsPublished, c.IncludeInMenu, c.ParentId, c.IsDeleted,
            })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            EntityType, items.Select(c => c.Id).ToList(), EnCulture, cancellationToken);

        var stamps = await _auditStamps.ReadAsync(
            nameof(Category), items.Select(c => c.Id).ToList(), cancellationToken);

        var dtos = items
            .Select(c => new AdminCategoryDto(
                c.Id, c.Name, overlay.Get(c.Id, LocalizedProperty.Name), c.Slug,
                c.Description, overlay.Get(c.Id, LocalizedProperty.Description),
                c.DisplayOrder, c.IsPublished, c.IncludeInMenu, c.ParentId, c.IsDeleted,
                stamps.CreatedBy(c.Id), stamps.ModifiedBy(c.Id)))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminCategoryDto>> Get(long id, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        var overlay = await _localization.GetOverlayAsync(EntityType, new[] { id }, EnCulture, cancellationToken);
        return Ok(ToDto(category, overlay.Get(id, LocalizedProperty.Name), overlay.Get(id, LocalizedProperty.Description)));
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

        await WriteEnglishAsync(category.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = category.Id },
            ToDto(category, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
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

        await WriteEnglishAsync(category.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(category, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
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
        category.Name = request.Name;
        category.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        category.Description = request.Description;
        category.MetaTitle = request.MetaTitle;
        category.MetaKeywords = request.MetaKeywords;
        category.MetaDescription = request.MetaDescription;
        category.DisplayOrder = request.DisplayOrder;
        category.IsPublished = request.IsPublished;
        category.IncludeInMenu = request.IncludeInMenu;
        category.ParentId = request.ParentId;
    }

    private async Task WriteEnglishAsync(long id, CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Description, EnCulture, request.DescriptionEn, cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AdminCategoryDto ToDto(Category c, string? nameEn, string? descriptionEn) => new(
        c.Id, c.Name, nameEn, c.Slug, c.Description, descriptionEn,
        c.DisplayOrder, c.IsPublished, c.IncludeInMenu, c.ParentId, c.IsDeleted);
}
