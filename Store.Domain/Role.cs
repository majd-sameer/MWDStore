using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

/// <summary>
/// Application role. Derives from <see cref="IdentityRole{TKey}"/>; the base supplies Name,
/// NormalizedName and ConcurrencyStamp.
/// </summary>
public class Role : IdentityRole<long>
{
    public ICollection<RoleClaim> RoleClaims { get; set; } = [];

    public ICollection<UserRole> Users { get; set; } = [];
}
