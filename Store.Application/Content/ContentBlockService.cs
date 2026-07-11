using Microsoft.EntityFrameworkCore;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Content;

public sealed class ContentBlockService : IContentBlockService
{
    private readonly StoreDbContext _db;
    private readonly IRequestCulture _culture;

    public ContentBlockService(StoreDbContext db, IRequestCulture culture)
    {
        _db = db;
        _culture = culture;
    }

    public async Task<IReadOnlyList<ContentBlockDto>> GetPublishedAsync(
        string? prefix, CancellationToken cancellationToken = default)
    {
        var query = _db.ContentBlocks.AsNoTracking().Where(b => b.IsPublished);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var withDot = prefix + ".";
            query = query.Where(b => b.Key == prefix || b.Key.StartsWith(withDot));
        }

        var blocks = await query.ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return [];
        }

        var lang = _culture.Language;
        return blocks
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new ContentBlockDto(
                b.Key,
                b.Title?.Resolve(lang),
                b.Text?.Resolve(lang),
                b.ImageUrl,
                b.LinkUrl,
                b.LinkText?.Resolve(lang),
                b.SortOrder))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminContentBlockDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var blocks = await _db.ContentBlocks.AsNoTracking()
            .ToListAsync(cancellationToken);

        return blocks
            .OrderBy(b => b.Key)
            .Select(ToAdminDto)
            .ToList();
    }

    public async Task<AdminContentBlockDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var block = await _db.ContentBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return block == null ? null : ToAdminDto(block);
    }

    public async Task<AdminContentBlockDto?> UpdateAsync(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (block == null)
        {
            return null;
        }

        block.Title = LocalizedString.From(request.Title, request.TitleEn);
        block.Text = LocalizedString.From(request.Text, request.TextEn);
        block.ImageUrl = request.ImageUrl;
        block.LinkUrl = request.LinkUrl;
        block.LinkText = LocalizedString.From(request.LinkText, request.LinkTextEn);
        block.SortOrder = request.SortOrder;
        block.IsPublished = request.IsPublished;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private static AdminContentBlockDto ToAdminDto(ContentBlock b) => new(
        b.Id, b.Key, b.Title?.Ar, b.Text?.Ar, b.ImageUrl, b.LinkUrl, b.LinkText?.Ar, b.SortOrder, b.IsPublished,
        b.Title?.En, b.Text?.En, b.LinkText?.En);
}
