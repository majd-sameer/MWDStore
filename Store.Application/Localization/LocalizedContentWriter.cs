using System.Linq.Expressions;
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

    /// <summary>
    /// Applies <see cref="SetAsync"/> once per (property, value) pair, in order, for one entity
    /// and culture. Staged like <see cref="SetAsync"/> — the caller still owns the save.
    /// </summary>
    Task SetManyAsync(
        string entityType,
        long entityId,
        string cultureId,
        IEnumerable<(string Property, string? Value)> values,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// String-keyed sibling of <see cref="SetAsync"/> for entities keyed by a code
    /// (<c>LocalizedContentProperty.EntityKey</c>, e.g. <c>Country</c> by ISO code).
    /// </summary>
    Task SetByKeyAsync(
        string entityType,
        string entityKey,
        string propertyName,
        string cultureId,
        string? value,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every overlay row for an entity — call when the entity is hard-deleted.</summary>
    Task RemoveAllAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    /// <summary>Removes every overlay row for a string-keyed entity — call on hard delete.</summary>
    Task RemoveAllByKeyAsync(string entityType, string entityKey, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class LocalizedContentWriter : ILocalizedContentWriter
{
    private readonly StoreDbContext _db;

    public LocalizedContentWriter(StoreDbContext db) => _db = db;

    public Task SetAsync(
        string entityType,
        long entityId,
        string propertyName,
        string cultureId,
        string? value,
        CancellationToken cancellationToken = default) =>
        SetCoreAsync(
            p => p.EntityType == entityType
                && p.EntityId == entityId
                && p.ProperyName == propertyName
                && p.CultureId == cultureId,
            () => new LocalizedContentProperty
            {
                EntityType = entityType,
                EntityId = entityId,
                CultureId = cultureId,
                ProperyName = propertyName,
                Value = value!,
            },
            value, cultureId, cancellationToken);

    public async Task SetManyAsync(
        string entityType,
        long entityId,
        string cultureId,
        IEnumerable<(string Property, string? Value)> values,
        CancellationToken cancellationToken = default)
    {
        foreach (var (property, value) in values)
        {
            await SetAsync(entityType, entityId, property, cultureId, value, cancellationToken);
        }
    }

    public Task SetByKeyAsync(
        string entityType,
        string entityKey,
        string propertyName,
        string cultureId,
        string? value,
        CancellationToken cancellationToken = default) =>
        SetCoreAsync(
            p => p.EntityType == entityType
                && p.EntityKey == entityKey
                && p.ProperyName == propertyName
                && p.CultureId == cultureId,
            () => new LocalizedContentProperty
            {
                EntityType = entityType,
                EntityKey = entityKey,
                CultureId = cultureId,
                ProperyName = propertyName,
                Value = value!,
            },
            value, cultureId, cancellationToken);

    public Task RemoveAllAsync(string entityType, long entityId, CancellationToken cancellationToken = default) =>
        RemoveWhereAsync(p => p.EntityType == entityType && p.EntityId == entityId, cancellationToken);

    public Task RemoveAllByKeyAsync(string entityType, string entityKey, CancellationToken cancellationToken = default) =>
        RemoveWhereAsync(p => p.EntityType == entityType && p.EntityKey == entityKey, cancellationToken);

    /// <summary>
    /// Upsert-or-remove for one overlay row. Only the lookup predicate and the new-row factory
    /// differ between the id-keyed and key-keyed entry points; <paramref name="createRow"/> is
    /// invoked only for a non-blank <paramref name="value"/>.
    /// </summary>
    private async Task SetCoreAsync(
        Expression<Func<LocalizedContentProperty, bool>> match,
        Func<LocalizedContentProperty> createRow,
        string? value,
        string cultureId,
        CancellationToken cancellationToken)
    {
        var row = await _db.LocalizedContentProperties.FirstOrDefaultAsync(match, cancellationToken);

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
            _db.LocalizedContentProperties.Add(createRow());
        }
        else
        {
            row.Value = value;
        }
    }

    private async Task RemoveWhereAsync(
        Expression<Func<LocalizedContentProperty, bool>> match, CancellationToken cancellationToken)
    {
        var rows = await _db.LocalizedContentProperties
            .Where(match)
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
