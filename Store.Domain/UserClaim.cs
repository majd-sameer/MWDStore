using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

public class UserClaim : IdentityUserClaim<long>
{
    public User User { get; set; } = null!;
}
