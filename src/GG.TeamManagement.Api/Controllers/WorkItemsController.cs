using GG.TeamManagement.Application.Boards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/work-items")]
public class WorkItemsController : ControllerBase
{
    private readonly BoardService _boards;

    public WorkItemsController(BoardService boards)
    {
        _boards = boards;
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> Update(
        Guid id,
        [FromBody] UpdateWorkItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _boards.UpdateWorkItemAsync(id, request, cancellationToken));
}
