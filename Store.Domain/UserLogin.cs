using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

public class UserLogin : IdentityUserLogin<long>
{
    public User User { get; set; } = null!;
}
