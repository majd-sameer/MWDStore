using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Application.Common;
using Store.Application.Content;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin editor for storefront content blocks — the words and images of designed sections. Only
/// <c>Value</c>, <c>MediumId</c>, <c>LinkUrl</c> and <c>IsActive</c> are editable; the key triple and
/// <c>Type</c> are code-owned (blocks ship via the seeder, so there is no create/delete here — that
/// is what guarantees "text yes, design no"). English is stored as a <c>LocalizedContentProperty</c>
/// overlay, exactly like product/news translations. Edits are audited automatically as
/// <c>EntityType = "ContentBlock"</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/content-blocks")]
public sealed class ContentBlocksController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;

    public ContentBlocksController(StoreDbContext db, TimeProvider timeProvider, IMediaUrlBuilder mediaUrl)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminContentSectionDto>>> List(
        [FromQuery] string page = "home", CancellationToken cancellationToken = default)
    {
        var blocks = await _db.ContentBlocks
            .Where(b => b.PageKey == page)
            .OrderBy(b => b.SectionKey).ThenBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new
            {
                b.Id,
                b.SectionKey,
                b.BlockKey,
                b.Type,
                b.Value,
                b.MediumId,
                b.LinkUrl,
                b.IsActive,
                b.SortOrder,
                MediumFileName = b.Medium != null ? b.Medium.FileName : null,
            })
            .ToListAsync(cancellationToken);

        var ids = blocks.Select(b => b.Id).ToList();
        var english = await _db.LocalizedContentProperties
            .AsNoTracking()
            .Where(p => p.EntityType == LocalizedEntity.ContentBlock
                && p.CultureId == RequestCulture.EnglishCultureId
                && p.ProperyName == LocalizedProperty.Value
                && ids.Contains(p.EntityId))
            .ToDictionaryAsync(p => p.EntityId, p => p.Value, cancellationToken);

        var groups = blocks
            .GroupBy(b => b.SectionKey)
            .Select(g => new AdminContentSectionDto(
                g.Key,
                g.Select(b => new AdminContentBlockDto(
                    b.Id, b.SectionKey, b.BlockKey, b.Type,
                    b.Value, english.GetValueOrDefault(b.Id),
                    b.MediumId, _mediaUrl.GetUrl(b.MediumFileName), b.LinkUrl,
                    b.IsActive, b.SortOrder)).ToList()))
            .ToList();

        return Ok(groups);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminContentBlockDto>> Update(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (block == null)
        {
            return NotFound();
        }

        var isRichText = string.Equals(block.Type, "richtext", StringComparison.OrdinalIgnoreCase);

        // BlockKey and Type are code-owned and absent from the request, so they can't be changed here.
        block.Value = isRichText ? ContentSanitizer.Sanitize(request.Value) : request.Value;
        block.MediumId = request.MediumId;
        block.LinkUrl = request.LinkUrl;
        block.IsActive = request.IsActive;
        block.UpdatedOn = _timeProvider.GetUtcNow();

        var englishValue = isRichText ? ContentSanitizer.Sanitize(request.ValueEn) : request.ValueEn;
        await UpsertEnglishAsync(id, englishValue, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var mediaUrl = block.MediumId is null
            ? null
            : _mediaUrl.GetUrl(await _db.Media
                .Where(m => m.Id == block.MediumId)
                .Select(m => m.FileName)
                .FirstOrDefaultAsync(cancellationToken));

        return Ok(new AdminContentBlockDto(
            block.Id, block.SectionKey, block.BlockKey, block.Type,
            block.Value, englishValue, block.MediumId, mediaUrl, block.LinkUrl, block.IsActive, block.SortOrder));
    }

    /// <summary>Inserts/updates/removes the en-US overlay row for a block's Value.</summary>
    private async Task UpsertEnglishAsync(long blockId, string? value, CancellationToken cancellationToken)
    {
        var row = await _db.LocalizedContentProperties.FirstOrDefaultAsync(
            p => p.EntityType == LocalizedEntity.ContentBlock
                && p.EntityId == blockId
                && p.ProperyName == LocalizedProperty.Value
                && p.CultureId == RequestCulture.EnglishCultureId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            if (row != null)
            {
                _db.LocalizedContentProperties.Remove(row);
            }

            return;
        }

        if (row == null)
        {
            if (!await _db.Cultures.AnyAsync(c => c.Id == RequestCulture.EnglishCultureId, cancellationToken))
            {
                _db.Cultures.Add(new Culture
                {
                    Id = RequestCulture.EnglishCultureId,
                    Name = RequestCulture.EnglishCultureId,
                });
            }

            _db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = LocalizedEntity.ContentBlock,
                EntityId = blockId,
                CultureId = RequestCulture.EnglishCultureId,
                ProperyName = LocalizedProperty.Value,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
    }
}

public sealed record AdminContentSectionDto(string SectionKey, IReadOnlyList<AdminContentBlockDto> Blocks);

public sealed record AdminContentBlockDto(
    long Id, string SectionKey, string BlockKey, string Type,
    string? ValueAr, string? ValueEn, long? MediumId, string? MediaUrl, string? LinkUrl,
    bool IsActive, int SortOrder);

/// <summary>Editable fields only — keys and Type are code-owned and cannot be changed.</summary>
public sealed class ContentBlockUpdateRequest
{
    /// <summary>Arabic (default) value.</summary>
    public string? Value { get; set; }

    /// <summary>English overlay value.</summary>
    public string? ValueEn { get; set; }

    public long? MediumId { get; set; }

    public string? LinkUrl { get; set; }

    public bool IsActive { get; set; } = true;
}
