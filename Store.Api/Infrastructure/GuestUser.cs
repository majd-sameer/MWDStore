namespace Store.Api.Infrastructure;

/// <summary>
/// The single shared system account that owns guest (no-login) orders. Guest checkout snapshots its
/// cart against this account's id; the shopper's real contact email is stored on the order
/// (<c>Order.GuestEmail</c>) and used as the shared secret on the public track lookup.
/// </summary>
public static class GuestUser
{
    public const string Email = "guest@store.local";

    public const string FullName = "Guest";
}
