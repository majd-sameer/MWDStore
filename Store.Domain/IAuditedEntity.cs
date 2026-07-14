namespace Store.Domain;

/// <summary>Entities carrying their own created/updated timestamps (per-actor history lives in the audit log).</summary>
public interface IAuditedEntity
{
    DateTimeOffset CreatedOn { get; set; }

    DateTimeOffset LatestUpdatedOn { get; set; }
}
