using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for menus and their items (old Cms module's menu admin).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/menus")]
public sealed class AdminMenusController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminMenusController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminMenuDto>>> List(CancellationToken cancellationToken)
    {
        var menus = await _db.Menus
            .Include(m => m.MenuItems)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        return Ok(menus.Select(ToDto).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminMenuDto>> Get(long id, CancellationToken cancellationToken)
    {
        var menu = await _db.Menus
            .Include(m => m.MenuItems)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return menu == null ? NotFound() : Ok(ToDto(menu));
    }

    [HttpPost]
    public async Task<ActionResult<AdminMenuDto>> Create(MenuUpsertRequest request, CancellationToken cancellationToken)
    {
        var menu = new Menu { Name = request.Name, IsPublished = request.IsPublished };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = menu.Id }, ToDto(menu));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminMenuDto>> Update(
        long id, MenuUpsertRequest request, CancellationToken cancellationToken)
    {
        var menu = await _db.Menus.Include(m => m.MenuItems).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (menu == null)
        {
            return NotFound();
        }

        menu.Name = request.Name;
        menu.IsPublished = request.IsPublished;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(menu));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var menu = await _db.Menus.Include(m => m.MenuItems).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (menu == null)
        {
            return NotFound();
        }

        if (menu.IsSystem)
        {
            return Conflict(new { error = "System menus cannot be deleted." });
        }

        _db.MenuItems.RemoveRange(menu.MenuItems);
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ----- Items ----------------------------------------------------------------------------------

    [HttpPost("{menuId:long}/items")]
    public async Task<ActionResult<AdminMenuItemDto>> AddItem(
        long menuId, MenuItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var menuExists = await _db.Menus.AnyAsync(m => m.Id == menuId, cancellationToken);
        if (!menuExists)
        {
            return NotFound();
        }

        var item = new MenuItem
        {
            MenuId = menuId,
            Name = request.Name,
            CustomLink = request.CustomLink,
            ParentId = request.ParentId,
            DisplayOrder = request.DisplayOrder
        };
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminMenuItemDto(item.Id, item.MenuId, item.ParentId, item.Name, item.CustomLink, item.DisplayOrder));
    }

    [HttpPut("{menuId:long}/items/{itemId:long}")]
    public async Task<ActionResult<AdminMenuItemDto>> UpdateItem(
        long menuId, long itemId, MenuItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == itemId && i.MenuId == menuId, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        item.Name = request.Name;
        item.CustomLink = request.CustomLink;
        item.ParentId = request.ParentId;
        item.DisplayOrder = request.DisplayOrder;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminMenuItemDto(item.Id, item.MenuId, item.ParentId, item.Name, item.CustomLink, item.DisplayOrder));
    }

    [HttpDelete("{menuId:long}/items/{itemId:long}")]
    public async Task<IActionResult> DeleteItem(long menuId, long itemId, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems
            .Include(i => i.InverseParent)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.MenuId == menuId, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        _db.MenuItems.RemoveRange(item.InverseParent);
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static AdminMenuDto ToDto(Menu m) => new(
        m.Id, m.Name, m.IsPublished, m.IsSystem,
        m.MenuItems
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new AdminMenuItemDto(i.Id, i.MenuId, i.ParentId, i.Name, i.CustomLink, i.DisplayOrder))
            .ToList());
}
