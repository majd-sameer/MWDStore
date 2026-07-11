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
/// Admin brand management (CRUD). Deletes are soft. <c>Name</c> and <c>Description</c> are bilingual:
/// Arabic in the base columns, English in the <c>LocalizedContentProperty</c> overlay (edited here as
/// <c>NameEn</c>/<c>DescriptionEn</c>, served to the storefront under <c>Accept-Language: en</c>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Catalog)]
[Route("api/admin/brands")]
public sealed class AdminBrandsController : ControllerBase
{
    private const string EntityType = LocalizedEntity.Brand;
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminBrandsController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBrandDto>>> List(
        [FromQuery] bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var brands = _db.Brands.AsQueryable();
        if (!includeDeleted)
        {
            brands = brands.Where(b => !b.IsDeleted);
        }

        var items = await brands
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.Slug, b.Description, b.IsPublished, b.IsDeleted })
            .ToListAsync(cancellationToken);

        var ids = items.Select(b => b.Id).ToList();
        var overlay = await _localization.GetOverlayAsync(EntityType, ids, EnCulture, cancellationToken);
        var stamps = await _auditStamps.ReadAsync(nameof(Brand), ids, cancellationToken);

        var dtos = items
            .Select(b => new AdminBrandDto(
                b.Id, b.Name, overlay.Get(b.Id, LocalizedProperty.Name), b.Slug,
                b.Description, overlay.Get(b.Id, LocalizedProperty.Description), b.IsPublished, b.IsDeleted,
                stamps.CreatedBy(b.Id), stamps.ModifiedBy(b.Id)))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminBrandDto>> Get(long id, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand == null)
        {
            return NotFound();
        }

        var overlay = await _localization.GetOverlayAsync(EntityType, new[] { id }, EnCulture, cancellationToken);
        return Ok(ToDto(brand, overlay.Get(id, LocalizedProperty.Name), overlay.Get(id, LocalizedProperty.Description)));
    }

    [HttpPost]
    public async Task<ActionResult<AdminBrandDto>> Create(BrandUpsertRequest request, CancellationToken cancellationToken)
    {
        var brand = new Brand();
        Apply(brand, request);

        if (await _db.Brands.AnyAsync(b => b.Slug == brand.Slug, cancellationToken))
        {
            return Conflict(new { error = $"A brand with slug '{brand.Slug}' already exists." });
        }

        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteEnglishAsync(brand.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = brand.Id },
            ToDto(brand, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminBrandDto>> Update(
        long id, BrandUpsertRequest request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand == null)
        {
            return NotFound();
        }

        Apply(brand, request);

        if (await _db.Brands.AnyAsync(b => b.Slug == brand.Slug && b.Id != id, cancellationToken))
        {
            return Conflict(new { error = $"A brand with slug '{brand.Slug}' already exists." });
        }

        await WriteEnglishAsync(brand.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(brand, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand == null)
        {
            return NotFound();
        }

        brand.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(Brand brand, BrandUpsertRequest request)
    {
        brand.Name = request.Name;
        brand.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        brand.Description = request.Description;
        brand.IsPublished = request.IsPublished;
    }

    private async Task WriteEnglishAsync(long id, BrandUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Description, EnCulture, request.DescriptionEn, cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AdminBrandDto ToDto(Brand b, string? nameEn, string? descriptionEn) =>
        new(b.Id, b.Name, nameEn, b.Slug, b.Description, descriptionEn, b.IsPublished, b.IsDeleted);
}
