using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for product options (Color, Size, ...) used to build variations.</summary>
[ApiController]
[RequirePermission(Permissions.CatalogManage)]
[Route("api/admin/product-options")]
public sealed class AdminProductOptionsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminProductOptionsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductOptionListItem>>> List(CancellationToken cancellationToken)
    {
        var options = await _db.ProductOptions
            .OrderBy(o => o.Name)
            .Select(o => new AdminProductOptionListItem(o.Id, o.Name))
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminProductOptionListItem>> Get(long id, CancellationToken cancellationToken)
    {
        var option = await _db.ProductOptions.FindAsync([id], cancellationToken);
        return option == null ? NotFound() : Ok(new AdminProductOptionListItem(option.Id, option.Name));
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductOptionListItem>> Create(
        ProductOptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var option = new ProductOption { Name = request.Name };
        _db.ProductOptions.Add(option);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = option.Id }, new AdminProductOptionListItem(option.Id, option.Name));
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
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminProductOptionListItem(option.Id, option.Name));
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

        _db.ProductOptions.Remove(option);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
