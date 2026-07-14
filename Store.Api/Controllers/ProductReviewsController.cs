using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>
/// Public product reviews: anyone reads approved reviews; signed-in customers submit reviews that
/// land in moderation as Pending (status 1).
/// </summary>
[ApiController]
[Route("api/products/{productId:long}/reviews")]
public sealed class ProductReviewsController : ControllerBase
{
    private const int PendingStatus = 1;
    private const int ApprovedStatus = 5;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ProductReviewsController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> List(
        long productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var reviews = await _db.Reviews
            .Where(r => r.EntityId == productId && r.EntityTypeId == "Product" && r.Status == ApprovedStatus)
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewDto(r.Id, r.Title, r.Comment, r.Rating, r.ReviewerName, r.CreatedOn))
            .ToListAsync(cancellationToken);

        return Ok(reviews);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> Submit(
        long productId, SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var productExists = await _db.Products.AnyAsync(
            p => p.Id == productId && p.IsPublished && !p.IsDeleted, cancellationToken);
        if (!productExists)
        {
            return NotFound();
        }

        var alreadyReviewed = await _db.Reviews.AnyAsync(
            r => r.EntityId == productId && r.EntityTypeId == "Product" && r.UserId == userId, cancellationToken);
        if (alreadyReviewed)
        {
            return Conflict(new { error = "You have already reviewed this product." });
        }

        var reviewerName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstAsync(cancellationToken);
        var review = new Review
        {
            UserId = userId,
            EntityId = productId,
            EntityTypeId = "Product",
            Title = request.Title,
            Comment = request.Comment,
            Rating = request.Rating,
            ReviewerName = reviewerName,
            Status = PendingStatus,
            CreatedOn = _timeProvider.GetUtcNow()
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ReviewDto(review.Id, review.Title, review.Comment, review.Rating, review.ReviewerName, review.CreatedOn));
    }
}
