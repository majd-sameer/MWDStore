using Microsoft.EntityFrameworkCore;
using Store.Data;

namespace Store.Application.Localization;

/// <inheritdoc />
public sealed class LocalizationService : ILocalizationService
{
    private readonly StoreDbContext _db;

    public LocalizationService(StoreDbContext db) => _db = db;

    public async Task<LocalizedOverlay> GetOverlayAsync(
        string entityType,
        IReadOnlyCollection<long> ids,
        string? cultureId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cultureId) || ids.Count == 0)
        {
            return LocalizedOverlay.Empty;
        }

        var rows = await _db.LocalizedContentProperties
            .AsNoTracking()
            .Where(p => p.EntityType == entityType
                && p.CultureId == cultureId
                && ids.Contains(p.EntityId)
                && p.Value != null)
            .Select(p => new { p.EntityId, p.ProperyName, p.Value })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return LocalizedOverlay.Empty;
        }

        var values = new Dictionary<(long, string), string>(rows.Count);
        foreach (var row in rows)
        {
            values[(row.EntityId, row.ProperyName)] = row.Value!;
        }

        return new LocalizedOverlay(values);
    }

    public async Task<LocalizedKeyOverlay> GetOverlayByKeyAsync(
        string entityType,
        IReadOnlyCollection<string> keys,
        string? cultureId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cultureId) || keys.Count == 0)
        {
            return LocalizedKeyOverlay.Empty;
        }

        var rows = await _db.LocalizedContentProperties
            .AsNoTracking()
            .Where(p => p.EntityType == entityType
                && p.CultureId == cultureId
                && p.EntityKey != null
                && keys.Contains(p.EntityKey)
                && p.Value != null)
            .Select(p => new { p.EntityKey, p.ProperyName, p.Value })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return LocalizedKeyOverlay.Empty;
        }

        var values = new Dictionary<(string, string), string>(rows.Count);
        foreach (var row in rows)
        {
            values[(row.EntityKey!, row.ProperyName)] = row.Value!;
        }

        return new LocalizedKeyOverlay(values);
    }
}
