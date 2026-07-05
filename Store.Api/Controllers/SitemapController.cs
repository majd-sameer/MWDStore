using System.Security;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Application.Payments;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>
/// Public <c>sitemap.xml</c>: home, the storefront listing page, published categories (as
/// <c>/shop?category=slug</c> — the storefront has no dedicated category route) and published,
/// individually-visible products (as <c>/products/{id}</c> — the storefront routes products by
/// id, not slug, so the sitemap must match the URLs that actually resolve).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("")]
public sealed class SitemapController : ControllerBase
{
    private const string ContentType = "application/xml; charset=utf-8";

    /// <summary>How long a built document is reused before the next request rebuilds it. A
    /// static field (not <c>IMemoryCache</c>) so no extra DI registration is needed — this is a
    /// single small string, and the controller is the only reader/writer.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly object CacheLock = new();
    private static string? _cachedXml;
    private static DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    private readonly StoreDbContext _db;
    private readonly PaymentsOptions _paymentsOptions;
    private readonly TimeProvider _timeProvider;

    public SitemapController(StoreDbContext db, PaymentsOptions paymentsOptions, TimeProvider timeProvider)
    {
        _db = db;
        _paymentsOptions = paymentsOptions;
        _timeProvider = timeProvider;
    }

    [HttpGet("sitemap.xml")]
    public async Task<ContentResult> Get(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        lock (CacheLock)
        {
            if (_cachedXml is not null && now - _cachedAt < CacheDuration)
            {
                return Content(_cachedXml, ContentType);
            }
        }

        var xml = await BuildAsync(cancellationToken);

        lock (CacheLock)
        {
            _cachedXml = xml;
            _cachedAt = now;
        }

        return Content(xml, ContentType);
    }

    private async Task<string> BuildAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _paymentsOptions.StorefrontBaseUrl.TrimEnd('/');

        var categorySlugs = await _db.Categories
            .Where(c => c.IsPublished && !c.IsDeleted)
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);

        var products = await _db.Products
            .Where(p => p.IsPublished && p.IsVisibleIndividually && !p.IsDeleted)
            .Select(p => new { p.Id, p.LatestUpdatedOn })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

        AppendUrl(sb, baseUrl, changefreq: "weekly", priority: "1.0");
        AppendUrl(sb, $"{baseUrl}/shop", changefreq: "daily", priority: "0.9");

        foreach (var slug in categorySlugs)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            AppendUrl(
                sb,
                $"{baseUrl}/shop?category={Uri.EscapeDataString(slug)}",
                changefreq: "weekly",
                priority: "0.7");
        }

        foreach (var product in products)
        {
            AppendUrl(
                sb,
                $"{baseUrl}/products/{product.Id}",
                changefreq: "weekly",
                priority: "0.8",
                lastmod: product.LatestUpdatedOn);
        }

        sb.Append("</urlset>\n");
        return sb.ToString();
    }

    private static void AppendUrl(
        StringBuilder sb, string loc, string changefreq, string priority, DateTimeOffset? lastmod = null)
    {
        sb.Append("  <url>\n");
        sb.Append("    <loc>").Append(SecurityElement.Escape(loc)).Append("</loc>\n");
        if (lastmod is { } value)
        {
            sb.Append("    <lastmod>").Append(value.UtcDateTime.ToString("yyyy-MM-dd")).Append("</lastmod>\n");
        }
        sb.Append("    <changefreq>").Append(changefreq).Append("</changefreq>\n");
        sb.Append("    <priority>").Append(priority).Append("</priority>\n");
        sb.Append("  </url>\n");
    }
}
