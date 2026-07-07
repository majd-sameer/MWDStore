using System;

namespace Store.Domain;

/// <summary>
/// Append-only record of a single admin mutation — who did what, when, and the before/after of the
/// changed fields. Rows are written by the audit action filter (on successful admin POST/PUT/DELETE)
/// and by explicit domain events (e.g. stock-out, login). There is deliberately no update or delete
/// path: the table only ever grows.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>Actor's user id, or null for system/seeder actions.</summary>
    public long? UserId { get; set; }

    /// <summary>Denormalized user name snapshot (the user may be renamed or soft-deleted later).</summary>
    public string UserName { get; set; } = null!;

    /// <summary>Primary role claim at the time of the action.</summary>
    public string Role { get; set; } = null!;

    /// <summary>"Create" | "Update" | "Delete" | "StockOut" | "Login" | custom.</summary>
    public string Action { get; set; } = null!;

    /// <summary>Entity CLR type name, e.g. "Product", "Order", "ContentBlock", "Stock".</summary>
    public string EntityType { get; set; } = null!;

    public long? EntityId { get; set; }

    /// <summary>Human-readable snapshot (product name, order number, …).</summary>
    public string? EntityName { get; set; }

    /// <summary>Changed properties only, before values (nvarchar(max)). Secrets are excluded.</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>Changed properties only, after values (nvarchar(max)). Secrets are excluded.</summary>
    public string? NewValuesJson { get; set; }

    /// <summary>Back-office area (maps to <c>AuthPolicies</c>): "Catalog", "Inventory", "Sales", …</summary>
    public string Area { get; set; } = null!;

    public string? IpAddress { get; set; }

    /// <summary>Correlation id from the X-Correlation-Id request header, when present.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>UTC timestamp of the action (indexed).</summary>
    public DateTime CreatedOn { get; set; }
}
