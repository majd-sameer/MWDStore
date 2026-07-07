namespace Store.Api.Infrastructure;

/// <summary>
/// Opts an admin action out of the automatic <see cref="AuditActionFilter"/>. Used by endpoints that
/// write their own richer audit entry — e.g. stock-out logs <c>Action = "StockOut"</c> explicitly
/// rather than the filter's generic verb-derived action.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipAuditAttribute : Attribute;
