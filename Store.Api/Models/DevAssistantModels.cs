using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

/// <summary>
/// A Developer Assistant query. The reply contract (AssistantReply and its content blocks) is defined
/// in Store.Application/DevAssistant and mirrored in data-access/models.ts — one commit, both sides
/// (hard rule 5). Length caps per SEC-5: the text is tokenized only, never evaluated.
/// </summary>
public sealed class DevAssistantQueryRequest
{
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The resolved subjects of up to the last 3 turns, most recent last — the bounded follow-up
    /// context of spec §3.6. No conversation state is ever kept on the server.
    /// </summary>
    [MaxLength(3)]
    public List<string>? ContextSubjects { get; set; }
}
