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
/// Admin CRUD for product options (Color, Size, ...) used to build variations. <c>Name</c> is
/// bilingual: Arabic in the base column, English in the <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Catalog)]
[Route("api/admin/product-options")]
public sealed class AdminProductOptionsController : ControllerBase
{
    private const string EntityType = LocalizedEntity.ProductOption;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminProductOptionsController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductOptionListItem>>> List(CancellationToken cancellationToken)
    {
        var options = await _db.ProductOptions
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.Name })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            EntityType, options.Select(o => o.Id).ToList(), RequestCulture.EnglishCultureId, cancellationToken);
        var stamps = await _auditStamps.ReadAsync(
            nameof(ProductOption), options.Select(o => o.Id).ToList(), cancellationToken);

        var dtos = options
            .Select(o => new AdminProductOptionListItem(
                o.Id, o.Name, overlay.Get(o.Id, LocalizedProperty.Name),
                stamps.CreatedBy(o.Id), stamps.ModifiedBy(o.Id)))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminProductOptionListItem>> Get(long id, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FindAsync([id], cancellationToken);
        if (option == null)
        {
            return NotFound();
        }

        var overlay = await _localization.GetOverlayAsync(EntityType, new[] { id }, RequestCulture.EnglishCultureId, cancellationToken);
        return Ok(new AdminProductOptionListItem(option.Id, option.Name, overlay.Get(id, LocalizedProperty.Name)));
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductOptionListItem>> Create(
        ProductOptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var option = new ProductOption { Name = request.Name };
        _db.ProductOptions.Add(option);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetAsync(EntityType, option.Id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = option.Id },
            new AdminProductOptionListItem(option.Id, option.Name, AdminText.NormalizeOrNull(request.NameEn)));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminProductOptionListItem>> Update(
        long id, ProductOptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FindAsync([id], cancellationToken);
        if (option == null)
        {
            return NotFound();
        }

        option.Name = request.Name;
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductOptionListItem(option.Id, option.Name, AdminText.NormalizeOrNull(request.NameEn)));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FindAsync([id], cancellationToken);
        if (option == null)
        {
            return NotFound();
        }

        var inUse = await _db.ProductOptionValues.AnyAsync(ov => ov.OptionId == id, cancellationToken)
            || await _db.ProductOptionCombinations.AnyAsync(c => c.OptionId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This option is used by one or more products and cannot be deleted." });
        }

        await _localizedWriter.RemoveAllAsync(EntityType, id, cancellationToken);
        _db.ProductOptions.Remove(option);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
