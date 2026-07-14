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
/// Admin CRUD for vendors. Deletes are soft. <c>Name</c> and <c>Description</c> are bilingual:
/// Arabic in the base columns, English in the <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Vendors)]
[Route("api/admin/vendors")]
public sealed class AdminVendorsController : ControllerBase
{
    private const string EntityType = LocalizedEntity.Vendor;
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminVendorsController(
        StoreDbContext db, TimeProvider timeProvider,
        ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _timeProvider = timeProvider;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminVendorDto>>> List(CancellationToken cancellationToken)
    {
        var vendors = await _db.Vendors
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.Name)
            .Select(v => new { v.Id, v.Name, v.Slug, v.Email, v.Description, v.IsActive })
            .ToListAsync(cancellationToken);

        var ids = vendors.Select(v => v.Id).ToList();
        var overlay = await _localization.GetOverlayAsync(EntityType, ids, EnCulture, cancellationToken);
        var stamps = await _auditStamps.ReadAsync(nameof(Vendor), ids, cancellationToken);

        var dtos = vendors
            .Select(v => new AdminVendorDto(
                v.Id, v.Name, overlay.Get(v.Id, LocalizedProperty.Name), v.Slug, v.Email,
                v.Description, overlay.Get(v.Id, LocalizedProperty.Description), v.IsActive,
                stamps.CreatedBy(v.Id), stamps.ModifiedBy(v.Id)))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<AdminVendorDto>> Create(VendorUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var vendor = new Vendor { CreatedOn = now, LatestUpdatedOn = now };
        Apply(vendor, request);
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteEnglishAsync(vendor.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(vendor, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminVendorDto>> Update(
        long id, VendorUpsertRequest request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor == null)
        {
            return NotFound();
        }

        Apply(vendor, request);
        vendor.LatestUpdatedOn = _timeProvider.GetUtcNow();

        await WriteEnglishAsync(vendor.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(vendor, Normalize(request.NameEn), Normalize(request.DescriptionEn)));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor == null)
        {
            return NotFound();
        }

        vendor.IsDeleted = true;
        vendor.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(Vendor vendor, VendorUpsertRequest request)
    {
        vendor.Name = request.Name;
        vendor.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        vendor.Email = request.Email;
        vendor.Description = request.Description;
        vendor.IsActive = request.IsActive;
    }

    private async Task WriteEnglishAsync(long id, VendorUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Description, EnCulture, request.DescriptionEn, cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AdminVendorDto ToDto(Vendor v, string? nameEn, string? descriptionEn) =>
        new(v.Id, v.Name, nameEn, v.Slug, v.Email, v.Description, descriptionEn, v.IsActive);
}
