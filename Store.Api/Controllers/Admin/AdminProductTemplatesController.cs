using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for product templates (named attribute sets used to prefill the product form).</summary>
[ApiController]
[RequirePermission(Permissions.CatalogManage)]
[Route("api/admin/product-templates")]
public sealed class AdminProductTemplatesController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminProductTemplatesController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductTemplateDto>>> List(CancellationToken cancellationToken)
    {
        var templates = await _db.ProductTemplates
            .Include(t => t.ProductAttributes).ThenInclude(a => a.Group)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return Ok(templates.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductTemplateDto>> Create(
        ProductTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var template = new ProductTemplate { Name = request.Name };
        await SetAttributesAsync(template, request.AttributeIds, cancellationToken);
        _db.ProductTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(template));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminProductTemplateDto>> Update(
        long id, ProductTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var template = await _db.ProductTemplates
            .Include(t => t.ProductAttributes).ThenInclude(a => a.Group)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template == null)
        {
            return NotFound();
        }

        template.Name = request.Name;
        await SetAttributesAsync(template, request.AttributeIds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(template));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var template = await _db.ProductTemplates
            .Include(t => t.ProductAttributes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template == null)
        {
            return NotFound();
        }

        template.ProductAttributes.Clear();
        _db.ProductTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task SetAttributesAsync(
        ProductTemplate template, IList<long> attributeIds, CancellationToken cancellationToken)
    {
        var attributes = await _db.ProductAttributes
            .Include(a => a.Group)
            .Where(a => attributeIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        template.ProductAttributes.Clear();
        foreach (var attribute in attributes)
        {
            template.ProductAttributes.Add(attribute);
        }
    }

    private static AdminProductTemplateDto ToDto(ProductTemplate t) => new(
        t.Id, t.Name,
        t.ProductAttributes
            .Select(a => new AdminProductAttributeDto(a.Id, a.Name, a.GroupId, a.Group.Name))
            .ToList());
}
