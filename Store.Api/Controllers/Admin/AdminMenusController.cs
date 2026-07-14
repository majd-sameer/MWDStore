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
/// Admin CRUD for menus and their items. Menu and menu-item <c>Name</c>s are bilingual: Arabic in
/// the base column, English in the <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/menus")]
public sealed class AdminMenusController : ControllerBase
{
    private const string MenuType = LocalizedEntity.Menu;
    private const string ItemType = LocalizedEntity.MenuItem;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminMenusController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminMenuDto>>> List(CancellationToken cancellationToken)
    {
        var menus = await _db.Menus
            .AsNoTracking()
            .Include(m => m.MenuItems)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var (menuOverlay, itemOverlay) = await LoadOverlaysAsync(menus, cancellationToken);

        var dtos = menus.Select(m => ToDto(m, menuOverlay, itemOverlay)).ToList();
        return Ok(await dtos.WithAuditStampsAsync(
            _auditStamps, nameof(Menu), d => d.Id,
            (d, createdBy, modifiedBy) => d with { CreatedBy = createdBy, ModifiedBy = modifiedBy },
            cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminMenuDto>> Get(long id, CancellationToken cancellationToken)
    {
        var menu = await _db.Menus
            .AsNoTracking()
            .Include(m => m.MenuItems)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (menu == null)
        {
            return NotFound();
        }

        var (menuOverlay, itemOverlay) = await LoadOverlaysAsync([menu], cancellationToken);
        return Ok(ToDto(menu, menuOverlay, itemOverlay));
    }

    [HttpPost]
    public async Task<ActionResult<AdminMenuDto>> Create(MenuUpsertRequest request, CancellationToken cancellationToken)
    {
        var menu = new Menu { Name = request.Name, IsPublished = request.IsPublished };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetAsync(MenuType, menu.Id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var (menuOverlay, itemOverlay) = await LoadOverlaysAsync([menu], cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = menu.Id }, ToDto(menu, menuOverlay, itemOverlay));
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
        await _localizedWriter.SetAsync(MenuType, id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var (menuOverlay, itemOverlay) = await LoadOverlaysAsync([menu], cancellationToken);
        return Ok(ToDto(menu, menuOverlay, itemOverlay));
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

        var itemIds = menu.MenuItems.Select(i => i.Id).ToList();
        var itemOverlays = await _db.LocalizedContentProperties
            .Where(p => p.EntityType == ItemType && itemIds.Contains(p.EntityId))
            .ToListAsync(cancellationToken);
        _db.LocalizedContentProperties.RemoveRange(itemOverlays);

        await _localizedWriter.RemoveAllAsync(MenuType, menu.Id, cancellationToken);
        _db.MenuItems.RemoveRange(menu.MenuItems);
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

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

        await _localizedWriter.SetAsync(ItemType, item.Id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminMenuItemDto(
            item.Id, item.MenuId, item.ParentId, item.Name, AdminText.NormalizeOrNull(request.NameEn), item.CustomLink, item.DisplayOrder));
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
        await _localizedWriter.SetAsync(ItemType, itemId, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminMenuItemDto(
            item.Id, item.MenuId, item.ParentId, item.Name, AdminText.NormalizeOrNull(request.NameEn), item.CustomLink, item.DisplayOrder));
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

        var overlayIds = item.InverseParent.Select(c => c.Id).Append(item.Id).ToList();
        var overlays = await _db.LocalizedContentProperties
            .Where(p => p.EntityType == ItemType && overlayIds.Contains(p.EntityId))
            .ToListAsync(cancellationToken);
        _db.LocalizedContentProperties.RemoveRange(overlays);
        _db.MenuItems.RemoveRange(item.InverseParent);
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<(LocalizedOverlay Menus, LocalizedOverlay Items)> LoadOverlaysAsync(
        IReadOnlyCollection<Menu> menus, CancellationToken cancellationToken)
    {
        var menuOverlay = await _localization.GetOverlayAsync(
            MenuType, menus.Select(m => m.Id).ToList(), RequestCulture.EnglishCultureId, cancellationToken);
        var itemIds = menus.SelectMany(m => m.MenuItems).Select(i => i.Id).ToList();
        var itemOverlay = await _localization.GetOverlayAsync(ItemType, itemIds, RequestCulture.EnglishCultureId, cancellationToken);
        return (menuOverlay, itemOverlay);
    }

    private static AdminMenuDto ToDto(Menu m, LocalizedOverlay menuOverlay, LocalizedOverlay itemOverlay) => new(
        m.Id, m.Name, menuOverlay.Get(m.Id, LocalizedProperty.Name), m.IsPublished, m.IsSystem,
        m.MenuItems
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new AdminMenuItemDto(
                i.Id, i.MenuId, i.ParentId, i.Name, itemOverlay.Get(i.Id, LocalizedProperty.Name),
                i.CustomLink, i.DisplayOrder))
            .ToList());
}
