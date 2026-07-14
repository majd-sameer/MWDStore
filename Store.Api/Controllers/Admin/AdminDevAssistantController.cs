using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.DevAssistant;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// The Developer Assistant portal (spec docs/DEV-ASSISTANT-PORTAL-SPEC.md): deterministic structural
/// answers computed from the deployed binary's own metadata. Reads metadata only, never rows —
/// the assistant's services cannot reach the DbContext from a query (SEC-9).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.DevAssistant)]
[Route("api/admin/dev-assistant")]
public sealed class AdminDevAssistantController : ControllerBase
{
    private const int MaxContextSubjectLength = 100;

    private readonly IDevAssistantService _assistant;
    private readonly IAuditService _audit;
    private readonly DevAssistantOptions _options;

    public AdminDevAssistantController(IDevAssistantService assistant, IAuditService audit, DevAssistantOptions options)
    {
        _assistant = assistant;
        _audit = audit;
        _options = options;
    }

    /// <summary>
    /// Submits a query. POST only because context makes the payload exceed comfortable query-string
    /// size — it is a read in effect, so it opts out of the generic audit filter and writes its own
    /// purpose-built entry per query (SEC-12), following the stock-out pattern.
    /// </summary>
    [HttpPost("query")]
    [SkipAudit]
    public async Task<ActionResult<AssistantReply>> Query(
        DevAssistantQueryRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return NotFound();

        if (request.ContextSubjects is not null && request.ContextSubjects.Any(s => s.Length > MaxContextSubjectLength))
            return BadRequest(new { error = "Context subjects are limited to 100 characters each." });

        var reply = _assistant.Query(request.Text, request.ContextSubjects, ResolveCulture());

        var actor = AuditActorFactory.FromContext(HttpContext);
        await _audit.LogAsync(new AuditEntry
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Role = actor.Role,
            Action = "DevAssistantQuery",
            EntityType = "DevAssistant",
            EntityName = Truncate(request.Text, 256),
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                intent = reply.Intent,
                subject = reply.Subject,
                hit = reply.Hit
            }),
            Area = "Dev-Assistant",
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId
        }, cancellationToken);

        return reply;
    }

    /// <summary>The capability catalog and snapshot fingerprint for the welcome card (spec §2.4).</summary>
    [HttpGet("capabilities")]
    public ActionResult<CapabilitiesReply> Capabilities()
    {
        if (!_options.Enabled)
            return NotFound();

        return _assistant.Capabilities(ResolveCulture());
    }

    /// <summary>
    /// "ar" when the request asks for Arabic (the SPA's acceptLanguage interceptor, or an explicit
    /// `culture` query param the resources send to stay language-reactive), otherwise "en".
    /// </summary>
    private string ResolveCulture()
    {
        var explicitCulture = Request.Query["culture"].ToString();
        var source = explicitCulture.Length > 0 ? explicitCulture : Request.Headers.AcceptLanguage.ToString();
        return source.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
