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
/// Admin CRUD for product attributes and their groups (spec sheet building blocks). Attribute and
/// group <c>Name</c>s are bilingual: Arabic in the base column, English in the
/// <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Catalog)]
[Route("api/admin/product-attributes")]
public sealed class AdminProductAttributesController : ControllerBase
{
    private const string AttributeType = LocalizedEntity.ProductAttribute;
    private const string GroupType = LocalizedEntity.ProductAttributeGroup;
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminProductAttributesController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductAttributeDto>>> List(CancellationToken cancellationToken)
    {
        var attributes = await _db.ProductAttributes
            .OrderBy(a => a.Group.Name).ThenBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.GroupId, GroupName = a.Group.Name })
            .ToListAsync(cancellationToken);

        var attrOverlay = await _localization.GetOverlayAsync(
            AttributeType, attributes.Select(a => a.Id).ToList(), EnCulture, cancellationToken);
        var groupOverlay = await _localization.GetOverlayAsync(
            GroupType, attributes.Select(a => a.GroupId).Distinct().ToList(), EnCulture, cancellationToken);
        var stamps = await _auditStamps.ReadAsync(
            nameof(ProductAttribute), attributes.Select(a => a.Id).ToList(), cancellationToken);

        var dtos = attributes
            .Select(a => new AdminProductAttributeDto(
                a.Id, a.Name, attrOverlay.Get(a.Id, LocalizedProperty.Name),
                a.GroupId, a.GroupName, groupOverlay.Get(a.GroupId, LocalizedProperty.Name),
                stamps.CreatedBy(a.Id), stamps.ModifiedBy(a.Id)))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminProductAttributeDto>> Get(long id, CancellationToken cancellationToken)
    {
        var attribute = await _db.ProductAttributes
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Name, a.GroupId, GroupName = a.Group.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (attribute == null)
        {
            return NotFound();
        }

        var attrOverlay = await _localization.GetOverlayAsync(AttributeType, new[] { id }, EnCulture, cancellationToken);
        var groupOverlay = await _localization.GetOverlayAsync(GroupType, new[] { attribute.GroupId }, EnCulture, cancellationToken);

        return Ok(new AdminProductAttributeDto(
            attribute.Id, attribute.Name, attrOverlay.Get(id, LocalizedProperty.Name),
            attribute.GroupId, attribute.GroupName, groupOverlay.Get(attribute.GroupId, LocalizedProperty.Name)));
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductAttributeDto>> Create(
        ProductAttributeUpsertRequest request, CancellationToken cancellationToken)
    {
        var group = await _db.Set<ProductAttributeGroup>().FindAsync([request.GroupId], cancellationToken);
        if (group == null)
        {
            return BadRequest(new { error = "The attribute group does not exist." });
        }

        var attribute = new ProductAttribute { Name = request.Name, GroupId = request.GroupId };
        _db.ProductAttributes.Add(attribute);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetAsync(AttributeType, attribute.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var groupOverlay = await _localization.GetOverlayAsync(GroupType, new[] { group.Id }, EnCulture, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = attribute.Id },
            new AdminProductAttributeDto(
                attribute.Id, attribute.Name, Normalize(request.NameEn),
                group.Id, group.Name, groupOverlay.Get(group.Id, LocalizedProperty.Name)));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminProductAttributeDto>> Update(
        long id, ProductAttributeUpsertRequest request, CancellationToken cancellationToken)
    {
        var attribute = await _db.ProductAttributes.FindAsync([id], cancellationToken);
        if (attribute == null)
        {
            return NotFound();
        }

        var group = await _db.Set<ProductAttributeGroup>().FindAsync([request.GroupId], cancellationToken);
        if (group == null)
        {
            return BadRequest(new { error = "The attribute group does not exist." });
        }

        attribute.Name = request.Name;
        attribute.GroupId = request.GroupId;
        await _localizedWriter.SetAsync(AttributeType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var groupOverlay = await _localization.GetOverlayAsync(GroupType, new[] { group.Id }, EnCulture, cancellationToken);

        return Ok(new AdminProductAttributeDto(
            attribute.Id, attribute.Name, Normalize(request.NameEn),
            group.Id, group.Name, groupOverlay.Get(group.Id, LocalizedProperty.Name)));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var attribute = await _db.ProductAttributes.FindAsync([id], cancellationToken);
        if (attribute == null)
        {
            return NotFound();
        }

        var inUse = await _db.ProductAttributeValues.AnyAsync(av => av.AttributeId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This attribute is used by one or more products and cannot be deleted." });
        }

        await _localizedWriter.RemoveAllAsync(AttributeType, id, cancellationToken);
        _db.ProductAttributes.Remove(attribute);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ----- Groups -------------------------------------------------------------------------------

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<AdminProductAttributeGroupDto>>> ListGroups(CancellationToken cancellationToken)
    {
        var groups = await _db.Set<ProductAttributeGroup>()
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            GroupType, groups.Select(g => g.Id).ToList(), EnCulture, cancellationToken);

        var dtos = groups
            .Select(g => new AdminProductAttributeGroupDto(g.Id, g.Name, overlay.Get(g.Id, LocalizedProperty.Name)))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("groups")]
    public async Task<ActionResult<AdminProductAttributeGroupDto>> CreateGroup(
        ProductAttributeGroupUpsertRequest request, CancellationToken cancellationToken)
    {
        var group = new ProductAttributeGroup { Name = request.Name };
        _db.Set<ProductAttributeGroup>().Add(group);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetAsync(GroupType, group.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductAttributeGroupDto(group.Id, group.Name, Normalize(request.NameEn)));
    }

    [HttpPut("groups/{id:long}")]
    public async Task<ActionResult<AdminProductAttributeGroupDto>> UpdateGroup(
        long id, ProductAttributeGroupUpsertRequest request, CancellationToken cancellationToken)
    {
        var group = await _db.Set<ProductAttributeGroup>().FindAsync([id], cancellationToken);
        if (group == null)
        {
            return NotFound();
        }

        group.Name = request.Name;
        await _localizedWriter.SetAsync(GroupType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductAttributeGroupDto(group.Id, group.Name, Normalize(request.NameEn)));
    }

    [HttpDelete("groups/{id:long}")]
    public async Task<IActionResult> DeleteGroup(long id, CancellationToken cancellationToken)
    {
        var group = await _db.Set<ProductAttributeGroup>().FindAsync([id], cancellationToken);
        if (group == null)
        {
            return NotFound();
        }

        var inUse = await _db.ProductAttributes.AnyAsync(a => a.GroupId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This group still contains attributes and cannot be deleted." });
        }

        await _localizedWriter.RemoveAllAsync(GroupType, id, cancellationToken);
        _db.Set<ProductAttributeGroup>().Remove(group);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
