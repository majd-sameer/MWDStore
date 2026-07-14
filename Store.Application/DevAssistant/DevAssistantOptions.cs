namespace Store.Application.DevAssistant;

/// <summary>
/// Host options for the Developer Assistant portal. Bound from the <c>DevAssistant</c> configuration
/// section; <see cref="Enabled"/> is the defense-in-depth kill switch (SEC-4) — when false the
/// endpoints return 404 without a code change.
/// </summary>
public sealed class DevAssistantOptions
{
    public const string SectionName = "DevAssistant";

    public bool Enabled { get; set; } = true;
}
