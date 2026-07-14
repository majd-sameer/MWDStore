namespace Store.Domain;

/// <summary>
/// Rows that are hidden rather than removed. There is deliberately no global query filter:
/// each query decides whether deleted rows are in scope (e.g. admin lists expose them via
/// <c>includeDeleted</c>).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
