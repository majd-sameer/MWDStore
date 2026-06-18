using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

public class RoleClaim : IdentityRoleClaim<long>
{
    public Role Role { get; set; } = null!;
}
