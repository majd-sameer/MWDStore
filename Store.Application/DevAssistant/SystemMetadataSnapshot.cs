namespace Store.Application.DevAssistant;

/// <summary>One scalar property of a mapped entity, as recorded from the EF model (spec §2.3 Source A).</summary>
public sealed record PropertySnapshot(
    string Name,
    string ClrType,
    string? SqlType,
    int? MaxLength,
    bool IsNullable,
    string? DefaultValue,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string? ForeignKeyPrincipal,
    bool IsIndexed,
    bool IsUnique,
    bool IsSensitive);

/// <summary>One navigation of a mapped entity.</summary>
public sealed record NavigationSnapshot(
    string Name,
    string TargetEntity,
    string Cardinality,
    string? DeleteBehavior);

/// <summary>One index of a mapped entity.</summary>
public sealed record IndexSnapshot(
    string? Name,
    IReadOnlyList<string> Columns,
    bool IsUnique,
    string? Filter);

/// <summary>
/// A convention-correlated source artifact for an entity/area (spec §2.3 Source C). <see cref="Verified"/>
/// means the backing type was confirmed by reflection; otherwise the path is convention-derived
/// ("expected") — used both for files to create and when a correlation could not be confirmed.
/// </summary>
public sealed record CorrelatedArtifact(
    string Layer,
    string Path,
    bool Verified,
    string Description);

/// <summary>One mapped entity type with everything the assistant may say about it.</summary>
public sealed record EntitySnapshot(
    string ClrName,
    string Namespace,
    string TableName,
    bool IsSoftDeletable,
    bool IsSeoEntity,
    bool IsAudited,
    bool IsBilingual,
    string? AdminAreaSegment,
    bool HasAdminController,
    bool HasConfigurationClass,
    IReadOnlyList<PropertySnapshot> Properties,
    IReadOnlyList<NavigationSnapshot> Navigations,
    IReadOnlyList<IndexSnapshot> Indexes);

/// <summary>
/// Identifies exactly which build is answering (spec §2.3): assembly informational version, a stable
/// hash over the ordered entity/property definitions, and the snapshot build time.
/// </summary>
public sealed record SnapshotFingerprint(
    string AssemblyVersion,
    string ModelHash,
    DateTimeOffset BuiltAt);

/// <summary>
/// The immutable structural truth the assistant answers from: EF model + reflected API surface +
/// convention correlations. Built once per process start; never mutated (SEC-13).
/// </summary>
public sealed record SystemMetadataSnapshot(
    SnapshotFingerprint Fingerprint,
    IReadOnlyList<EntitySnapshot> Entities,
    IReadOnlyList<ApiEndpointDescriptor> Endpoints,
    bool? HasPendingMigrations,
    IReadOnlyList<string> Notices)
{
    public EntitySnapshot? FindEntity(string clrName) =>
        Entities.FirstOrDefault(e => string.Equals(e.ClrName, clrName, StringComparison.OrdinalIgnoreCase));
}
