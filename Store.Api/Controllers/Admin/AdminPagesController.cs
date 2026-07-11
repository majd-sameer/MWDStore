using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for CMS pages (old Cms module's page admin). Deletes are soft. Name/Body/SEO
/// fields are bilingual <see cref="LocalizedString"/> values (Arabic in the base column, English in
/// the sibling "...En" column).</summary>
[ApiController]
[RequirePermission(Permissions.ContentManage)]
[Route("api/admin/pages")]
public sealed class AdminPagesController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminPagesController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPageDto>>> List(CancellationToken cancellationToken)
    {
        var pages = await _db.Pages
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        return Ok(pages.Select(ToDto).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminPageDto>> Get(long id, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        return Ok(ToDto(page));
    }

    [HttpPost]
    public async Task<ActionResult<AdminPageDto>> Create(PageUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();
        var page = new Page
        {
            CreatedById = userId,
            CreatedOn = now,
            LatestUpdatedById = userId,
            LatestUpdatedOn = now
        };
        Apply(page, request, now);

        if (await _db.Pages.AnyAsync(p => p.Slug == page.Slug && !p.IsDeleted, cancellationToken))
        {
            return Conflict(new { error = $"A page with slug '{page.Slug}' already exists." });
        }

        _db.Pages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = page.Id }, ToDto(page));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminPageDto>> Update(
        long id, PageUpsertRequest request, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        var now = _timeProvider.GetUtcNow();
        Apply(page, request, now);
        page.LatestUpdatedById = User.GetUserId();
        page.LatestUpdatedOn = now;

        if (await _db.Pages.AnyAsync(p => p.Slug == page.Slug && p.Id != id && !p.IsDeleted, cancellationToken))
        {
            return Conflict(new { error = $"A page with slug '{page.Slug}' already exists." });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(page));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        page.IsDeleted = true;
        page.LatestUpdatedById = User.GetUserId();
        page.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(Page page, PageUpsertRequest request, DateTimeOffset now)
    {
        page.Name = new LocalizedString(request.Name, request.NameEn);
        page.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        page.Body = LocalizedString.From(request.Body, request.BodyEn);
        page.MetaTitle = LocalizedString.From(request.MetaTitle, request.MetaTitleEn);
        page.MetaKeywords = LocalizedString.From(request.MetaKeywords, request.MetaKeywordsEn);
        page.MetaDescription = LocalizedString.From(request.MetaDescription, request.MetaDescriptionEn);
        page.IsPublished = request.IsPublished;
        page.PublishedOn ??= request.IsPublished ? now : null;
    }

    private static AdminPageDto ToDto(Page p) => new(
        p.Id, p.Name.Ar!, p.Slug, p.Body?.Ar, p.MetaTitle?.Ar, p.MetaKeywords?.Ar, p.MetaDescription?.Ar,
        p.IsPublished, p.PublishedOn, p.CreatedOn,
        p.Name.En, p.Body?.En, p.MetaTitle?.En, p.MetaKeywords?.En, p.MetaDescription?.En,
        HasEnglish: p.Name.En != null || p.Body?.En != null || p.MetaTitle?.En != null
            || p.MetaKeywords?.En != null || p.MetaDescription?.En != null);
}
