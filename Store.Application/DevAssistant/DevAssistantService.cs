namespace Store.Application.DevAssistant;

/// <summary>The assistant's application service: one query in, one structured reply out.
/// <paramref name="culture"/> is "en" or "ar" and selects the language of the composed answer;
/// the metadata itself (paths, types, routes) is language-neutral.</summary>
public interface IDevAssistantService
{
    AssistantReply Query(string text, IReadOnlyList<string>? contextSubjects = null, string culture = "en");

    CapabilitiesReply Capabilities(string culture = "en");
}

/// <summary>
/// Stateless orchestrator over the immutable snapshot: intent resolution → composition. The engine
/// and composer are built once per snapshot (i.e. once per process) and shared thereafter.
/// </summary>
public sealed class DevAssistantService : IDevAssistantService
{
    private readonly ISystemMetadataProvider _metadata;
    private readonly Lazy<(IntentEngine Engine, AnswerComposer Composer)> _pipeline;

    public DevAssistantService(ISystemMetadataProvider metadata)
    {
        _metadata = metadata;
        _pipeline = new Lazy<(IntentEngine, AnswerComposer)>(() =>
        {
            var engine = new IntentEngine(_metadata.Snapshot);
            return (engine, new AnswerComposer(_metadata.Snapshot, engine));
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public AssistantReply Query(string text, IReadOnlyList<string>? contextSubjects = null, string culture = "en")
    {
        var (engine, composer) = _pipeline.Value;
        var resolution = engine.Resolve(text, contextSubjects);
        return composer.Compose(text, resolution, culture);
    }

    public CapabilitiesReply Capabilities(string culture = "en") => _pipeline.Value.Composer.CapabilityCatalog(culture);
}
