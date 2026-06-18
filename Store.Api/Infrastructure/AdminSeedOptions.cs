namespace Store.Api.Infrastructure;

/// <summary>
/// Configures the bootstrap admin account (bound from the <c>AdminUser</c> config section). The password is a
/// secret — keep it in the gitignored <c>appsettings.Development.json</c> or user-secrets, never in source.
/// Seeding is skipped when no password is configured.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminUser";

    public string Email { get; set; } = "admin@mystore.local";

    public string FullName { get; set; } = "Store Administrator";

    public string? Password { get; set; }
}
