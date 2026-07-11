using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Application.Auditing;
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
/// is what guarantees "text yes, design no"). The one exception is the FAQ list, a genuinely
/// repeatable section: <see cref="AddFaqQuestion"/> / <see cref="DeleteFaqQuestion"/> append and
/// remove <c>q{n}</c>/<c>a{n}</c> pairs, which adds words (not design). English is stored as a
/// <c>LocalizedContentProperty</c> overlay, exactly like product/news translations. Edits are audited
/// automatically as <c>EntityType = "ContentBlock"</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/content-blocks")]
public sealed class ContentBlocksController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;
    private readonly IAuditStampReader _auditStamps;

    public ContentBlocksController(
        StoreDbContext db, TimeProvider timeProvider, IMediaUrlBuilder mediaUrl, IAuditStampReader auditStamps)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
        _auditStamps = auditStamps;
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

        var stamps = await _auditStamps.ReadAsync(nameof(ContentBlock), ids, cancellationToken);

        var groups = blocks
            .GroupBy(b => b.SectionKey)
            .Select(g => new AdminContentSectionDto(
                g.Key,
                g.Select(b => new AdminContentBlockDto(
                    b.Id, b.SectionKey, b.BlockKey, b.Type,
                    b.Value, english.GetValueOrDefault(b.Id),
                    b.MediumId, _mediaUrl.GetUrl(b.MediumFileName), b.LinkUrl,
                    b.IsActive, b.SortOrder,
                    stamps.CreatedBy(b.Id), stamps.ModifiedBy(b.Id))).ToList()))
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

    /// <summary>The FAQ list is the one repeatable section where questions may be added/removed.</summary>
    private const string FaqPage = "faq";
    private const string FaqSection = "faq-list";

    /// <summary>
    /// Appends a new FAQ question/answer pair (<c>q{n}</c>/<c>a{n}</c>, both "text"). FAQ is the only
    /// repeatable list section, so — unlike every other block — items may be created here. The design
    /// is unchanged; the storefront renders the new pair with the same template.
    /// </summary>
    [HttpPost("faq-questions")]
    public async Task<ActionResult<FaqQuestionDto>> AddFaqQuestion(
        FaqQuestionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionAr) && string.IsNullOrWhiteSpace(request.QuestionEn))
        {
            return BadRequest(new { error = "A question is required." });
        }

        var existing = await _db.ContentBlocks
            .Where(b => b.PageKey == FaqPage && b.SectionKey == FaqSection)
            .Select(b => new { b.BlockKey, b.SortOrder })
            .ToListAsync(cancellationToken);

        var nextIndex = existing
            .Select(b => ParseIndex(b.BlockKey))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var nextSort = existing.Select(b => b.SortOrder).DefaultIfEmpty(-1).Max() + 1;

        var now = _timeProvider.GetUtcNow();
        var question = new ContentBlock
        {
            PageKey = FaqPage, SectionKey = FaqSection, BlockKey = $"q{nextIndex}",
            Type = "text", Value = request.QuestionAr, IsActive = true,
            SortOrder = nextSort, CreatedOn = now, UpdatedOn = now,
        };
        var answer = new ContentBlock
        {
            PageKey = FaqPage, SectionKey = FaqSection, BlockKey = $"a{nextIndex}",
            Type = "text", Value = request.AnswerAr, IsActive = true,
            SortOrder = nextSort + 1, CreatedOn = now, UpdatedOn = now,
        };
        _db.ContentBlocks.Add(question);
        _db.ContentBlocks.Add(answer);
        await _db.SaveChangesAsync(cancellationToken);

        await UpsertEnglishAsync(question.Id, request.QuestionEn, cancellationToken);
        await UpsertEnglishAsync(answer.Id, request.AnswerEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new FaqQuestionDto(nextIndex, question.Id, answer.Id));
    }

    /// <summary>Removes an FAQ question/answer pair (both blocks and their English overlays).</summary>
    [HttpDelete("faq-questions/{index:int}")]
    public async Task<IActionResult> DeleteFaqQuestion(int index, CancellationToken cancellationToken)
    {
        var blocks = await _db.ContentBlocks
            .Where(b => b.PageKey == FaqPage && b.SectionKey == FaqSection
                && (b.BlockKey == $"q{index}" || b.BlockKey == $"a{index}"))
            .ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return NotFound();
        }

        var ids = blocks.Select(b => b.Id).ToList();
        var overlays = await _db.LocalizedContentProperties
            .Where(p => p.EntityType == LocalizedEntity.ContentBlock && ids.Contains(p.EntityId))
            .ToListAsync(cancellationToken);

        _db.LocalizedContentProperties.RemoveRange(overlays);
        _db.ContentBlocks.RemoveRange(blocks);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Numeric suffix of a "q7"/"a7" key; null when it has none.</summary>
    private static int? ParseIndex(string blockKey) =>
        blockKey.Length > 1 && int.TryParse(blockKey.AsSpan(1), out var n) ? n : null;

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
    bool IsActive, int SortOrder, string? CreatedBy = null, string? ModifiedBy = null);

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

/// <summary>Identity of a newly added FAQ pair (its index and the two block ids).</summary>
public sealed record FaqQuestionDto(int Index, long QuestionId, long AnswerId);

/// <summary>A new FAQ question/answer pair, in both languages (English optional).</summary>
public sealed class FaqQuestionRequest
{
    public string? QuestionAr { get; set; }

    public string? QuestionEn { get; set; }

    public string? AnswerAr { get; set; }

    public string? AnswerEn { get; set; }
}
