using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;


public sealed record WishListItemDto(
    long Id, long ProductId, string ProductName, string ProductSlug, decimal Price,
    string? ThumbnailUrl, int Quantity, bool IsAvailable);

public sealed record WishListDto(long Id, IReadOnlyList<WishListItemDto> Items);

public sealed class AddWishListItemRequest
{
    [Required]
    public long ProductId { get; set; }

    public int Quantity { get; set; } = 1;
}

// Reviews

public sealed record ReviewDto(
    long Id, string? Title, string? Comment, int Rating, string? ReviewerName, DateTimeOffset CreatedOn);

public sealed class SubmitReviewRequest
{
    public string? Title { get; set; }

    [Required]
    public string Comment { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }
}

// CMS pages & news

public sealed record PublicPageDto(
    string Name, string Slug, string? Body, string? MetaTitle, string? MetaKeywords, string? MetaDescription);

public sealed record NewsListItemDto(
    long Id, string Name, string Slug, string? ShortContent, string? ThumbnailUrl, DateTimeOffset? PublishedOn,
    string? CategorySlug = null);

public sealed record NewsDetailDto(
    long Id, string Name, string Slug, string? ShortContent, string? FullContent, string? ThumbnailUrl,
    string? MetaTitle, string? MetaKeywords, string? MetaDescription, DateTimeOffset? PublishedOn,
    string? CategorySlug = null, NewsLinkedProductDto? Product = null);

public sealed record NewsLinkedProductDto(
    long Id, string Name, string Slug, decimal Price, string? ThumbnailUrl);

public sealed record AlertDto(
    long Id, string Slug, string Name, string? ShortContent, string? AlertCtaUrl);



public sealed record ContactAreaPublicDto(long Id, string Name);

public sealed class SubmitContactRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public long ContactAreaId { get; set; }
}

// Comparison & recently viewed

public sealed record ComparisonAttributeDto(string Name, string? Value);

public sealed record ComparisonProductDto(
    long ProductId, string Name, string Slug, decimal Price, string? ThumbnailUrl,
    IReadOnlyList<ComparisonAttributeDto> Attributes);

public sealed class AddComparisonRequest
{
    [Required]
    public long ProductId { get; set; }
}

public sealed record RecentlyViewedDto(
    long ProductId, string Name, string Slug, decimal Price, string? ThumbnailUrl, DateTimeOffset LatestViewedOn);

public sealed class RecordViewRequest
{
    [Required]
    public long ProductId { get; set; }
}
