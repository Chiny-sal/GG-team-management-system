using GG.TeamManagement.Application.Deletions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/deletion-requests")]
public class DeletionRequestsController : ControllerBase
{
    private readonly DeletionRequestService _deletions;

    public DeletionRequestsController(DeletionRequestService deletions)
    {
        _deletions = deletions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeletionRequestDto>>> Pending(CancellationToken cancellationToken) =>
        Ok(await _deletions.GetPendingAsync(cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await _deletions.ApproveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _deletions.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
