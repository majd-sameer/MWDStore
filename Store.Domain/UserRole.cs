using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

/// <summary>
/// The user-role join (replaces the implicit many-to-many). Derives from
/// <see cref="IdentityUserRole{TKey}"/>, which supplies the <c>UserId</c>/<c>RoleId</c> composite key.
/// </summary>
public class UserRole : IdentityUserRole<long>
{
    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
