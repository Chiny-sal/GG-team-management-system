using GG.TeamManagement.Application.Boards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly BoardService _boards;

    public GroupsController(BoardService boards)
    {
        _boards = boards;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _boards.GetGroupsAsync(cancellationToken));
}
