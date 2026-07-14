using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// The account operations shared by the customer and staff admin screens (which split one user
/// table by staff role). The screens' differing guards — staff-protection on the customer side,
/// self-deletion on the user side — stay in the controllers; only the actions behind them live here.
/// </summary>
public static class UserAdminSupport
{
    /// <summary>A new account shell. The caller assigns roles and persists it via Identity.</summary>
    public static User BuildUser(string email, string fullName, string? phoneNumber, DateTimeOffset now) => new()
    {
        UserName = email,
        Email = email,
        FullName = fullName,
        PhoneNumber = phoneNumber,
        UserGuid = Guid.NewGuid(),
        CreatedOn = now,
        LatestUpdatedOn = now
    };

    /// <summary>Replaces the user's customer-group links with exactly <paramref name="groupIds"/>. Does not save.</summary>
    public static async Task SetCustomerGroupsAsync(
        StoreDbContext db, long userId, IList<long> groupIds, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.CustomerGroups)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var groups = await db.CustomerGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        user.CustomerGroups.Clear();
        foreach (var group in groups)
        {
            user.CustomerGroups.Add(group);
        }
    }

    /// <summary>Soft-deletes the account and locks sign-in permanently. Saves.</summary>
    public static async Task SoftDeleteAsync(
        StoreDbContext db, User user, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.LatestUpdatedOn = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }
}
