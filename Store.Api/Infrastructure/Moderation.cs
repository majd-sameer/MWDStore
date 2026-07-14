namespace Store.Api.Infrastructure;

/// <summary>
/// Moderation status values shared by comment and review moderation. The numeric values mirror
/// the SimplCommerce data this store was migrated from, so existing rows keep their meaning.
/// </summary>
public static class Moderation
{
    public const int Pending = 1;
    public const int Approved = 5;
    public const int NotApproved = 8;

    public static readonly int[] ValidStatuses = [Pending, Approved, NotApproved];

    public const string InvalidStatusError = "Status must be 1 (Pending), 5 (Approved) or 8 (NotApproved).";
}
