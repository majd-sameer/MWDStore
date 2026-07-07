using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for warehouses (each owns an <see cref="Address"/> row, like the old Inventory module).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Inventory)]
[Route("api/admin/warehouses")]
public sealed class AdminWarehousesController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminWarehousesController(StoreDbContext db)
    {
        _db = db;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Warehouse, AdminWarehouseDto>> Projection =
        w => new AdminWarehouseDto(
            w.Id, w.Name, w.Address.ContactName, w.Address.Phone, w.Address.AddressLine1, w.Address.AddressLine2,
            w.Address.City, w.Address.ZipCode, w.Address.StateOrProvinceId, w.Address.StateOrProvince.Name,
            w.Address.CountryId, w.Address.Country.Name);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminWarehouseDto>>> List(CancellationToken cancellationToken)
    {
        var warehouses = await _db.Warehouses
            .OrderBy(w => w.Name)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return Ok(warehouses);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminWarehouseDto>> Get(long id, CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses
            .Where(w => w.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouse == null ? NotFound() : Ok(warehouse);
    }

    [HttpPost]
    public async Task<ActionResult<AdminWarehouseDto>> Create(
        WarehouseUpsertRequest request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            Name = request.Name,
            Address = new Address()
        };
        Apply(warehouse, request);

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = warehouse.Id }, await LoadDtoAsync(warehouse.Id, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminWarehouseDto>> Update(
        long id, WarehouseUpsertRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses
            .Include(w => w.Address)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse == null)
        {
            return NotFound();
        }

        Apply(warehouse, request);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await LoadDtoAsync(id, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var warehouse = await _db.Warehouses
            .Include(w => w.Address)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse == null)
        {
            return NotFound();
        }

        var hasStock = await _db.Stocks.AnyAsync(s => s.WarehouseId == id && s.Quantity > 0, cancellationToken);
        if (hasStock)
        {
            return Conflict(new { error = "This warehouse still holds stock and cannot be deleted." });
        }

        var stocks = await _db.Stocks.Where(s => s.WarehouseId == id).ToListAsync(cancellationToken);
        _db.Stocks.RemoveRange(stocks);
        _db.Warehouses.Remove(warehouse);
        _db.Addresses.Remove(warehouse.Address);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(Warehouse warehouse, WarehouseUpsertRequest request)
    {
        warehouse.Name = request.Name;
        warehouse.Address.ContactName = request.ContactName;
        warehouse.Address.Phone = request.Phone;
        warehouse.Address.AddressLine1 = request.AddressLine1;
        warehouse.Address.AddressLine2 = request.AddressLine2;
        warehouse.Address.City = request.City;
        warehouse.Address.ZipCode = request.ZipCode;
        warehouse.Address.StateOrProvinceId = request.StateOrProvinceId;
        warehouse.Address.CountryId = request.CountryId;
    }

    private Task<AdminWarehouseDto?> LoadDtoAsync(long id, CancellationToken cancellationToken) =>
        _db.Warehouses
            .Where(w => w.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken)!;
}
