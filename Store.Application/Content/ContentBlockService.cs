using Microsoft.EntityFrameworkCore;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Content;

/// <summary>Entity-type/property-name discriminators used to store <c>ContentBlock</c> English
/// overrides in <c>LocalizedContentProperty</c>. Kept local to this feature (rather than added to
/// the shared <see cref="Store.Application.Localization.LocalizedEntity"/>/<see cref="LocalizedProperty"/>
/// registries) since <c>ILocalizationService.GetOverlayAsync</c> takes both as plain strings.</summary>
internal static class ContentBlockLocalization
{
    public const string EntityType = "ContentBlock";
    public const string Title = "Title";
    public const string Text = "Text";
    public const string LinkText = "LinkText";

    /// <summary>Matches <c>Store.Api.Infrastructure.RequestCulture.EnglishCultureId</c> (Application
    /// cannot reference Api, so the value is duplicated as a plain constant here — both sides are
    /// just the <c>Culture.Id</c> row for English).</summary>
    public const string EnglishCultureId = "en-US";
}

public sealed class ContentBlockService : IContentBlockService
{
    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private bool _englishCultureChecked;

    public ContentBlockService(StoreDbContext db, ILocalizationService localization)
    {
        _db = db;
        _localization = localization;
    }

    public async Task<IReadOnlyList<ContentBlockDto>> GetPublishedAsync(
        string? prefix, string? cultureId, CancellationToken cancellationToken = default)
    {
        var query = _db.ContentBlocks.AsNoTracking().Where(b => b.IsPublished);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var withDot = prefix + ".";
            query = query.Where(b => b.Key == prefix || b.Key.StartsWith(withDot));
        }

        var blocks = await query.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return [];
        }

        var overlay = await _localization.GetOverlayAsync(
            ContentBlockLocalization.EntityType, blocks.Select(b => b.Id).ToList(), cultureId, cancellationToken);

        return blocks.Select(b => new ContentBlockDto(
            b.Key,
            overlay.Apply(b.Id, ContentBlockLocalization.Title, b.Title),
            overlay.Apply(b.Id, ContentBlockLocalization.Text, b.Text),
            b.ImageUrl,
            b.LinkUrl,
            overlay.Apply(b.Id, ContentBlockLocalization.LinkText, b.LinkText),
            b.SortOrder)).ToList();
    }

    public async Task<IReadOnlyList<AdminContentBlockDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var blocks = await _db.ContentBlocks.AsNoTracking()
            .OrderBy(b => b.Key)
            .ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return [];
        }

        var overlay = await _localization.GetOverlayAsync(
            ContentBlockLocalization.EntityType, blocks.Select(b => b.Id).ToList(),
            ContentBlockLocalization.EnglishCultureId, cancellationToken);

        return blocks.Select(b => ToAdminDto(b, overlay)).ToList();
    }

    public async Task<AdminContentBlockDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var block = await _db.ContentBlocks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (block == null)
        {
            return null;
        }

        var overlay = await _localization.GetOverlayAsync(
            ContentBlockLocalization.EntityType, new[] { block.Id }, ContentBlockLocalization.EnglishCultureId,
            cancellationToken);
        return ToAdminDto(block, overlay);
    }

    public async Task<AdminContentBlockDto?> UpdateAsync(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (block == null)
        {
            return null;
        }

        block.Title = request.Title;
        block.Text = request.Text;
        block.ImageUrl = request.ImageUrl;
        block.LinkUrl = request.LinkUrl;
        block.LinkText = request.LinkText;
        block.SortOrder = request.SortOrder;
        block.IsPublished = request.IsPublished;

        await UpsertLocalizedAsync(block.Id, ContentBlockLocalization.Title, request.TitleEn, cancellationToken);
        await UpsertLocalizedAsync(block.Id, ContentBlockLocalization.Text, request.TextEn, cancellationToken);
        await UpsertLocalizedAsync(block.Id, ContentBlockLocalization.LinkText, request.LinkTextEn, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private static AdminContentBlockDto ToAdminDto(ContentBlock b, LocalizedOverlay overlay) => new(
        b.Id, b.Key, b.Title, b.Text, b.ImageUrl, b.LinkUrl, b.LinkText, b.SortOrder, b.IsPublished,
        overlay.Get(b.Id, ContentBlockLocalization.Title),
        overlay.Get(b.Id, ContentBlockLocalization.Text),
        overlay.Get(b.Id, ContentBlockLocalization.LinkText));

    /// <summary>Upserts (by EntityType+EntityId+CultureId+ProperyName) the English overlay row for
    /// one property, mirroring <c>LocalizationSeeder</c>'s upsert pattern. A null/empty value is only
    /// written when a row already exists (so "clear the translation" round-trips); it never creates
    /// an empty row.</summary>
    private async Task UpsertLocalizedAsync(
        long entityId, string property, string? value, CancellationToken cancellationToken)
    {
        var row = await _db.LocalizedContentProperties.FirstOrDefaultAsync(
            p => p.EntityType == ContentBlockLocalization.EntityType
                && p.EntityId == entityId
                && p.CultureId == ContentBlockLocalization.EnglishCultureId
                && p.ProperyName == property,
            cancellationToken);

        if (row == null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            await EnsureEnglishCultureAsync(cancellationToken);
            _db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = ContentBlockLocalization.EntityType,
                EntityId = entityId,
                CultureId = ContentBlockLocalization.EnglishCultureId,
                ProperyName = property,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
    }

    private async Task EnsureEnglishCultureAsync(CancellationToken cancellationToken)
    {
        if (_englishCultureChecked)
        {
            return;
        }

        if (!await _db.Cultures.AnyAsync(c => c.Id == ContentBlockLocalization.EnglishCultureId, cancellationToken))
        {
            _db.Cultures.Add(new Culture
            {
                Id = ContentBlockLocalization.EnglishCultureId,
                Name = ContentBlockLocalization.EnglishCultureId,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        _englishCultureChecked = true;
    }
}
