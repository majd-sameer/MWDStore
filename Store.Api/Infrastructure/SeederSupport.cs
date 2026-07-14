using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>Small helpers shared by the startup seeders.</summary>
public static class SeederSupport
{
    /// <summary>
    /// Inserts the culture row if it is missing and saves immediately when it inserted.
    /// Seeder-only: <c>LocalizedContentWriter</c> has its own non-saving variant because there the
    /// caller commits the culture together with the overlay rows in one transaction.
    /// </summary>
    public static async Task EnsureCultureAsync(
        StoreDbContext db, string cultureId, CancellationToken cancellationToken)
    {
        if (!await db.Cultures.AnyAsync(c => c.Id == cultureId, cancellationToken))
        {
            db.Cultures.Add(new Culture { Id = cultureId, Name = cultureId });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
