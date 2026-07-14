using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Common;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's wishlist.</summary>
[ApiController]
[Authorize]
[Route("api/wishlist")]
public sealed class WishlistController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;

    public WishlistController(StoreDbContext db, TimeProvider timeProvider, IMediaUrlBuilder mediaUrl)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
    }

    [HttpGet]
    public async Task<ActionResult<WishListDto>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var wishList = await _db.WishLists
            .AsNoTracking()
            .Include(w => w.WishListItems).ThenInclude(i => i.Product).ThenInclude(p => p.ThumbnailImage)
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

        if (wishList == null)
        {
            return Ok(new WishListDto(0, []));
        }

        return Ok(ToDto(wishList));
    }

    [HttpPost("items")]
    public async Task<ActionResult<WishListDto>> AddItem(
        AddWishListItemRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var now = _timeProvider.GetUtcNow();

        var productExists = await _db.Products.AnyAsync(
            p => p.Id == request.ProductId && p.IsPublished && !p.IsDeleted, cancellationToken);
        if (!productExists)
        {
            return BadRequest(new { error = "The product does not exist." });
        }

        var wishList = await _db.WishLists
            .Include(w => w.WishListItems)
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

        if (wishList == null)
        {
            wishList = new WishList { UserId = userId, CreatedOn = now, LatestUpdatedOn = now };
            _db.WishLists.Add(wishList);
        }

        var item = wishList.WishListItems.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (item == null)
        {
            wishList.WishListItems.Add(new WishListItem
            {
                ProductId = request.ProductId,
                Quantity = Math.Max(request.Quantity, 1),
                CreatedOn = now,
                LatestUpdatedOn = now
            });
        }
        else
        {
            item.Quantity = Math.Max(request.Quantity, 1);
            item.LatestUpdatedOn = now;
        }

        wishList.LatestUpdatedOn = now;
        await _db.SaveChangesAsync(cancellationToken);

        var reloaded = await _db.WishLists
            .AsNoTracking()
            .Include(w => w.WishListItems).ThenInclude(i => i.Product).ThenInclude(p => p.ThumbnailImage)
            .FirstAsync(w => w.UserId == userId, cancellationToken);

        return Ok(ToDto(reloaded));
    }

    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> RemoveItem(long itemId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var item = await _db.WishListItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.WishList.UserId == userId, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        _db.WishListItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private WishListDto ToDto(WishList wishList) => new(
        wishList.Id,
        wishList.WishListItems
            .OrderByDescending(i => i.Id)
            .Select(i => new WishListItemDto(
                i.Id, i.ProductId, i.Product.Name, i.Product.Slug, i.Product.Price,
                _mediaUrl.GetUrl(i.Product.ThumbnailImage?.FileName), i.Quantity,
                i.Product.IsPublished && !i.Product.IsDeleted && i.Product.IsAllowToOrder))
            .ToList());
}
