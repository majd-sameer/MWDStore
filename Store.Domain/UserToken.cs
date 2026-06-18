using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

public class UserToken : IdentityUserToken<long>
{
    public User User { get; set; } = null!;
}
