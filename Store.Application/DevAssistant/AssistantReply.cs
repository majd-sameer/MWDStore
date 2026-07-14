using System.Text.Json.Serialization;

namespace Store.Application.DevAssistant;

/// <summary>
/// The structured reply contract (spec §2.5): a reply is an ordered sequence of typed content blocks.
/// Serialized polymorphically with a <c>type</c> discriminator; every block carries a plain-text
/// <see cref="Summary"/> so an older client can render a graceful fallback (FR-UI-7). These records
/// are a public contract mirrored one-to-one in <c>web/projects/data-access/src/lib/models.ts</c> —
/// change both sides in one commit (hard rule 5).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ChecklistBlock), "checklist")]
[JsonDerivedType(typeof(PropertyGridBlock), "propertyGrid")]
[JsonDerivedType(typeof(EndpointMatrixBlock), "endpointMatrix")]
[JsonDerivedType(typeof(CalloutBlock), "callout")]
[JsonDerivedType(typeof(SuggestionsBlock), "suggestions")]
public abstract record AnswerBlock(string Summary);

/// <summary>A template-composed sentence or two. Never free-form generated prose.</summary>
public sealed record TextBlock(string Summary, string Text) : AnswerBlock(Summary);

/// <summary>A guardrail attached to the checklist step it protects (FR-UI-12).</summary>
public sealed record StepWarning(string Severity, string RuleId, string Text, string? DocRef);

/// <summary>One step of a file-modification checklist.</summary>
public sealed record ChecklistStep(
    string Layer,
    string? FilePath,
    bool Verified,
    string Description,
    string? Command,
    IReadOnlyList<StepWarning> Warnings);

/// <summary>Ordered file-modification steps; <see cref="Interactive"/> is false for locate answers.</summary>
public sealed record ChecklistBlock(
    string Summary,
    string Title,
    bool Interactive,
    IReadOnlyList<ChecklistStep> Steps) : AnswerBlock(Summary);

/// <summary>One column row of a property grid. Sensitive rows carry name only (SEC-10).</summary>
public sealed record PropertyGridRow(
    string Name,
    string? ClrType,
    string? SqlType,
    int? MaxLength,
    bool? Nullable,
    string? DefaultValue,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string? ForeignKeyPrincipal,
    bool IsIndexed,
    bool IsUnique,
    bool IsSensitive);

/// <summary>One relationship row (RelationQuery variant of the grid).</summary>
public sealed record RelationRow(
    string Direction,
    string Name,
    string RelatedEntity,
    string Cardinality,
    string? DeleteBehavior);

/// <summary>Entity header + column rows straight from the EF model snapshot.</summary>
public sealed record PropertyGridBlock(
    string Summary,
    string EntityName,
    string TableName,
    IReadOnlyList<string> Markers,
    bool IsBilingual,
    IReadOnlyList<PropertyGridRow> Rows,
    IReadOnlyList<RelationRow> Relations) : AnswerBlock(Summary);

/// <summary>One API action row.</summary>
public sealed record EndpointRow(
    string Verb,
    string Route,
    string Action,
    string? Policy,
    bool Audited);

/// <summary>The routing matrix for an area, from the reflected API surface.</summary>
public sealed record EndpointMatrixBlock(
    string Summary,
    string Area,
    IReadOnlyList<EndpointRow> Rows) : AnswerBlock(Summary);

/// <summary>A highlighted guardrail card (severity: info | warning | critical).</summary>
public sealed record CalloutBlock(
    string Summary,
    string Severity,
    string? RuleId,
    string Text,
    string? DocRef) : AnswerBlock(Summary);

/// <summary>A tappable alternative query.</summary>
public sealed record SuggestionChip(string Label, string Query);

/// <summary>Ranked alternative queries, for misses and ambiguity.</summary>
public sealed record SuggestionsBlock(
    string Summary,
    IReadOnlyList<SuggestionChip> Items) : AnswerBlock(Summary);

/// <summary>The complete answer to one query.</summary>
public sealed record AssistantReply(
    string Intent,
    string? Subject,
    bool Hit,
    bool SubjectCarriedOver,
    SnapshotFingerprint Fingerprint,
    IReadOnlyList<AnswerBlock> Blocks);

/// <summary>One supported intent, for the capability catalog.</summary>
public sealed record CapabilityDto(string Intent, string Description, IReadOnlyList<string> Examples);

/// <summary>The capability catalog + fingerprint returned by GET capabilities (spec §2.4).</summary>
public sealed record CapabilitiesReply(
    SnapshotFingerprint Fingerprint,
    IReadOnlyList<CapabilityDto> Capabilities,
    IReadOnlyList<string> Notices);
