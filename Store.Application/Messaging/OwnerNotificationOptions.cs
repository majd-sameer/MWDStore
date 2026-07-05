namespace Store.Application.Messaging;

/// <summary>
/// Store-owner contact used for the "owner copy" of order-lifecycle transactional emails. Bound from the
/// <c>AdminUser</c> configuration section in <c>Store.Api</c> (the same section the admin bootstrap account
/// reads its email from) — falls back to the placeholder default when the section is absent.
/// </summary>
public sealed class OwnerNotificationOptions
{
    public const string SectionName = "AdminUser";

    public string Email { get; set; } = "admin@mystore.local";
}
