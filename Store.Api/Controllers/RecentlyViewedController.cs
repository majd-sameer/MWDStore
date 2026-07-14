using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Common;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's recently viewed products.</summary>
[ApiController]
[Authorize]
[Route("api/recently-viewed")]
public sealed class RecentlyViewedController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;

    public RecentlyViewedController(StoreDbContext db, TimeProvider timeProvider, IMediaUrlBuilder mediaUrl)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecentlyViewedDto>>> Get(
        [FromQuery] int count = 8, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 24);
        var userId = User.GetUserId();

        var rows = await _db.RecentlyViewedProducts
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LatestViewedOn)
            .Take(count)
            .Join(_db.Products.Where(p => p.IsPublished && !p.IsDeleted),
                r => r.ProductId, p => p.Id,
                (r, p) => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Price,
                    ThumbnailFileName = p.ThumbnailImage != null ? p.ThumbnailImage.FileName : null,
                    r.LatestViewedOn
                })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new RecentlyViewedDto(
                x.Id, x.Name, x.Slug, x.Price,
                _mediaUrl.GetUrl(x.ThumbnailFileName),
                x.LatestViewedOn))
            .ToList();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Record(RecordViewRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var now = _timeProvider.GetUtcNow();

        var row = await _db.RecentlyViewedProducts
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == request.ProductId, cancellationToken);
        if (row == null)
        {
            _db.RecentlyViewedProducts.Add(new RecentlyViewedProduct
            {
                UserId = userId,
                ProductId = request.ProductId,
                LatestViewedOn = now
            });
        }
        else
        {
            row.LatestViewedOn = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
