using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin brand management (CRUD). Deletes are soft. Brand name/description are bilingual
/// <see cref="LocalizedString"/> values (Arabic in the base column, English in the sibling
/// "...En" column).</summary>
[ApiController]
[RequirePermission(Permissions.CatalogManage)]
[Route("api/admin/brands")]
public sealed class AdminBrandsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminBrandsController(StoreDbContext db)
    {
        _db = db;
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

        var rows = await brands
            .OrderBy(b => b.Name.Ar)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminBrandDto>> Get(long id, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand == null)
        {
            return NotFound();
        }

        return Ok(ToDto(brand));
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

        return CreatedAtAction(nameof(Get), new { id = brand.Id }, ToDto(brand));
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

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(brand));
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
        brand.Name = new LocalizedString(request.Name, request.NameEn);
        brand.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        brand.Description = LocalizedString.From(request.Description, request.DescriptionEn);
        brand.IsPublished = request.IsPublished;
    }

    private static AdminBrandDto ToDto(Brand b) => new(
        b.Id, b.Name.Ar!, b.Slug, b.Description?.Ar, b.IsPublished, b.IsDeleted,
        b.Name.En, b.Description?.En,
        HasEnglish: b.Name.En != null || b.Description?.En != null);
}
