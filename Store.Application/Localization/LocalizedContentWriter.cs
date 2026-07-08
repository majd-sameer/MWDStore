using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Localization;

/// <summary>
/// Admin write-side companion to <see cref="ILocalizationService"/>: upserts and removes the
/// per-culture overlay rows in <c>LocalizedContentProperty</c> (the base entity columns hold the
/// default-culture text — Arabic for this catalog — and these rows carry another culture, e.g.
/// <c>en-US</c>). A blank value removes the overlay so the base value shows through again.
/// </summary>
public interface ILocalizedContentWriter
{
    /// <summary>
    /// Upserts the overlay for one entity property/culture, or removes it when
    /// <paramref name="value"/> is null/whitespace. The target <see cref="Culture"/> row is created
    /// on demand. Changes are staged on the shared <c>StoreDbContext</c>; the caller commits with its
    /// own <c>SaveChangesAsync</c> so the entity and its overlays persist in one transaction.
    /// </summary>
    Task SetAsync(
        string entityType,
        long entityId,
        string propertyName,
        string cultureId,
        string? value,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every overlay row for an entity — call when the entity is hard-deleted.</summary>
    Task RemoveAllAsync(string entityType, long entityId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class LocalizedContentWriter : ILocalizedContentWriter
{
    private readonly StoreDbContext _db;

    public LocalizedContentWriter(StoreDbContext db) => _db = db;

    public async Task SetAsync(
        string entityType,
        long entityId,
        string propertyName,
        string cultureId,
        string? value,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.LocalizedContentProperties.FirstOrDefaultAsync(
            p => p.EntityType == entityType
                && p.EntityId == entityId
                && p.ProperyName == propertyName
                && p.CultureId == cultureId,
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
            await EnsureCultureAsync(cultureId, cancellationToken);
            _db.LocalizedContentProperties.Add(new LocalizedContentProperty
            {
                EntityType = entityType,
                EntityId = entityId,
                CultureId = cultureId,
                ProperyName = propertyName,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
    }

    public async Task RemoveAllAsync(string entityType, long entityId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.LocalizedContentProperties
            .Where(p => p.EntityType == entityType && p.EntityId == entityId)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            _db.LocalizedContentProperties.RemoveRange(rows);
        }
    }

    private async Task EnsureCultureAsync(string cultureId, CancellationToken cancellationToken)
    {
        if (!await _db.Cultures.AnyAsync(c => c.Id == cultureId, cancellationToken))
        {
            _db.Cultures.Add(new Culture { Id = cultureId, Name = cultureId });
        }
    }
}
