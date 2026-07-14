namespace Store.Application.DevAssistant;

/// <summary>
/// One controller action harvested from the API assembly: the live equivalent of the handoff
/// document's route catalog, guaranteed to match the deployed binary (spec §2.3 Source B).
/// </summary>
public sealed record ApiEndpointDescriptor(
    string Controller,
    string Action,
    string Verb,
    string Route,
    string? Policy,
    bool AllowAnonymous,
    bool SkipAudit,
    string? RequestType,
    string? ResponseType);

/// <summary>
/// Abstraction over the reflection scan of the <c>Store.Api</c> assembly. Implemented in the API
/// layer (which can see its own controller types) and consumed by <see cref="SystemMetadataProvider"/>
/// here, so the Application layer never references ASP.NET Core MVC types.
/// </summary>
public interface IApiSurfaceSource
{
    /// <summary>Informational version of the scanned assembly, for the snapshot fingerprint.</summary>
    string AssemblyVersion { get; }

    /// <summary>
    /// Scans the API assembly once. Failures on individual controllers must degrade to
    /// <paramref name="notices"/> entries, never throw (SEC-15).
    /// </summary>
    IReadOnlyList<ApiEndpointDescriptor> Scan(ICollection<string> notices);
}
