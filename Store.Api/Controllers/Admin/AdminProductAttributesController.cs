using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for product attributes and their groups (spec sheet building blocks).</summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/product-attributes")]
public sealed class AdminProductAttributesController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminProductAttributesController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductAttributeDto>>> List(CancellationToken cancellationToken)
    {
        var attributes = await _db.ProductAttributes
            .OrderBy(a => a.Group.Name).ThenBy(a => a.Name)
            .Select(a => new AdminProductAttributeDto(a.Id, a.Name, a.GroupId, a.Group.Name))
            .ToListAsync(cancellationToken);

        return Ok(attributes);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminProductAttributeDto>> Get(long id, CancellationToken cancellationToken)
    {
        var attribute = await _db.ProductAttributes
            .Where(a => a.Id == id)
            .Select(a => new AdminProductAttributeDto(a.Id, a.Name, a.GroupId, a.Group.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return attribute == null ? NotFound() : Ok(attribute);
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

        return CreatedAtAction(nameof(Get), new { id = attribute.Id },
            new AdminProductAttributeDto(attribute.Id, attribute.Name, group.Id, group.Name));
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
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductAttributeDto(attribute.Id, attribute.Name, group.Id, group.Name));
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
            .Select(g => new AdminProductAttributeGroupDto(g.Id, g.Name))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpPost("groups")]
    public async Task<ActionResult<AdminProductAttributeGroupDto>> CreateGroup(
        ProductAttributeGroupUpsertRequest request, CancellationToken cancellationToken)
    {
        var group = new ProductAttributeGroup { Name = request.Name };
        _db.Set<ProductAttributeGroup>().Add(group);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductAttributeGroupDto(group.Id, group.Name));
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
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductAttributeGroupDto(group.Id, group.Name));
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

        _db.Set<ProductAttributeGroup>().Remove(group);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
