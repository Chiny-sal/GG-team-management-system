using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Boards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public class BoardsController : ControllerBase
{
    private readonly BoardService _boards;
    private readonly IBoardExportService _export;
    private readonly ICurrentWeekService _currentWeek;

    public BoardsController(BoardService boards, IBoardExportService export, ICurrentWeekService currentWeek)
    {
        _boards = boards;
        _export = export;
        _currentWeek = currentWeek;
    }

    [HttpGet("{groupId:guid}")]
    public async Task<ActionResult<BoardDto>> Get(Guid groupId, [FromQuery] DateOnly? weekId, CancellationToken cancellationToken) =>
        Ok(await _boards.GetBoardAsync(groupId, weekId, cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPost("{groupId:guid}/work-items")]
    public async Task<ActionResult<WorkItemDto>> CreateWorkItem(
        Guid groupId,
        [FromBody] CreateWorkItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _boards.CreateWorkItemAsync(groupId, request, cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPost("{groupId:guid}/save")]
    public async Task<ActionResult<SaveBoardResponse>> Save(Guid groupId, CancellationToken cancellationToken) =>
        Ok(await _boards.SaveBoardAsync(groupId, cancellationToken));

    [HttpGet("{groupId:guid}/export")]
    public async Task<IActionResult> Export(Guid groupId, [FromQuery] DateOnly? weekId, CancellationToken cancellationToken)
    {
        await _boards.GetBoardAsync(groupId, weekId, cancellationToken);
        var week = weekId ?? _currentWeek.GetCurrentWeekId();
        var bytes = await _export.ExportAsync(groupId, week, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"board-{groupId}-{week:yyyy-MM-dd}.xlsx");
    }
}
