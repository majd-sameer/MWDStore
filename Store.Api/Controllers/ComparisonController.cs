using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Common;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's product comparison list (old ProductComparison module, max 4 products).</summary>
[ApiController]
[Authorize]
[Route("api/comparison")]
public sealed class ComparisonController : ControllerBase
{
    private const int MaxProducts = 4;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;

    public ComparisonController(StoreDbContext db, TimeProvider timeProvider, IMediaUrlBuilder mediaUrl)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComparisonProductDto>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var comparing = await _db.ComparingProducts
            .Include(c => c.Product).ThenInclude(p => p.ThumbnailImage)
            .Include(c => c.Product).ThenInclude(p => p.ProductAttributeValues).ThenInclude(a => a.Attribute)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var items = comparing.Select(c => new ComparisonProductDto(
            c.ProductId, c.Product.Name, c.Product.Slug, c.Product.Price,
            _mediaUrl.GetUrl(c.Product.ThumbnailImage?.FileName),
            c.Product.ProductAttributeValues
                .Select(a => new ComparisonAttributeDto(a.Attribute.Name, a.Value))
                .ToList()))
            .ToList();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddComparisonRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var exists = await _db.ComparingProducts.AnyAsync(
            c => c.UserId == userId && c.ProductId == request.ProductId, cancellationToken);
        if (exists)
        {
            return NoContent();
        }

        var count = await _db.ComparingProducts.CountAsync(c => c.UserId == userId, cancellationToken);
        if (count >= MaxProducts)
        {
            return Conflict(new { error = $"You can compare at most {MaxProducts} products." });
        }

        var productExists = await _db.Products.AnyAsync(
            p => p.Id == request.ProductId && p.IsPublished && !p.IsDeleted, cancellationToken);
        if (!productExists)
        {
            return BadRequest(new { error = "The product does not exist." });
        }

        _db.ComparingProducts.Add(new ComparingProduct
        {
            UserId = userId,
            ProductId = request.ProductId,
            CreatedOn = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{productId:long}")]
    public async Task<IActionResult> Remove(long productId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var row = await _db.ComparingProducts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId, cancellationToken);
        if (row == null)
        {
            return NotFound();
        }

        _db.ComparingProducts.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
