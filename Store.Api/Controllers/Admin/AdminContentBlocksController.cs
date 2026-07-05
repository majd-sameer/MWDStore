using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Application.Content;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin read/update for homepage <c>ContentBlock</c> rows (hero, mission/story, values,
/// CTA band, ...). The set of blocks is fixed by the seeder — no create/delete here, only editing
/// the base (Arabic) fields and the English overlay in one call.</summary>
[ApiController]
[RequirePermission(Permissions.ContentManage)]
[Route("api/admin/content-blocks")]
public sealed class AdminContentBlocksController : ControllerBase
{
    private readonly IContentBlockService _contentBlocks;

    public AdminContentBlocksController(IContentBlockService contentBlocks)
    {
        _contentBlocks = contentBlocks;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminContentBlockDto>>> List(CancellationToken cancellationToken)
    {
        var blocks = await _contentBlocks.ListAsync(cancellationToken);
        return Ok(blocks);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminContentBlockDto>> Get(long id, CancellationToken cancellationToken)
    {
        var block = await _contentBlocks.GetAsync(id, cancellationToken);
        return block == null ? NotFound() : Ok(block);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminContentBlockDto>> Update(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken)
    {
        var updated = await _contentBlocks.UpdateAsync(id, request, cancellationToken);
        return updated == null ? NotFound() : Ok(updated);
    }
}
