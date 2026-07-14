using Store.Api.Models;
using Store.Application.Auditing;

namespace Store.Api.Infrastructure;

/// <summary>
/// Overlays "created by" / "modified by" audit stamps onto already-projected admin list DTOs,
/// with one batched <see cref="IAuditStampReader.ReadAsync"/> call per list. Ids missing from the
/// audit trail resolve to nulls, so <paramref name="apply"/> runs unconditionally for every item.
/// </summary>
public static class AuditStampExtensions
{
    public static async Task<PagedResult<T>> WithAuditStampsAsync<T>(
        this PagedResult<T> result,
        IAuditStampReader reader,
        string entityType,
        Func<T, long> id,
        Func<T, string?, string?, T> apply,
        CancellationToken cancellationToken)
    {
        var items = await result.Items.WithAuditStampsAsync(reader, entityType, id, apply, cancellationToken);
        return result with { Items = items };
    }

    public static async Task<List<T>> WithAuditStampsAsync<T>(
        this IReadOnlyList<T> items,
        IAuditStampReader reader,
        string entityType,
        Func<T, long> id,
        Func<T, string?, string?, T> apply,
        CancellationToken cancellationToken)
    {
        var stamps = await reader.ReadAsync(entityType, items.Select(id).ToList(), cancellationToken);
        return items
            .Select(x => apply(x, stamps.CreatedBy(id(x)), stamps.ModifiedBy(id(x))))
            .ToList();
    }
}
