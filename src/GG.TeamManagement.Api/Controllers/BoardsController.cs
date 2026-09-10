using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain;
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
    public async Task<ActionResult<BoardDto>> Get(
        Guid groupId,
        [FromQuery] DateOnly? weekId,
        [FromQuery] string? period,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken) =>
        Ok(await _boards.GetBoardAsync(groupId, weekId, period, year, month, cancellationToken));

    [HttpGet("{groupId:guid}/registry")]
    public async Task<ActionResult<WorkRegistryDto>> Registry(
        Guid groupId,
        CancellationToken cancellationToken) =>
        Ok(await _boards.GetRegistryAsync(groupId, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.LeadOrOfficeManagement)]
    [HttpPost("{groupId:guid}/work-items")]
    public async Task<ActionResult<WorkItemDto>> CreateWorkItem(
        Guid groupId,
        [FromBody] CreateWorkItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _boards.CreateWorkItemAsync(groupId, request, cancellationToken));

    [HttpPost("{groupId:guid}/commit")]
    public async Task<IActionResult> Commit(
        Guid groupId,
        [FromBody] CommitBoardRequest request,
        CancellationToken cancellationToken)
    {
        await _boards.CommitBoardAsync(groupId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/save")]
    public async Task<IActionResult> Save(
        Guid groupId,
        [FromBody] CommitBoardRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is not null)
            await _boards.CommitBoardAsync(groupId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{groupId:guid}/export")]
    public async Task<IActionResult> Export(
        Guid groupId,
        [FromQuery] DateOnly? weekId,
        [FromQuery] string? period,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        await _boards.GetBoardAsync(groupId, weekId, period, year, month, cancellationToken);
        var timePeriod = TimePeriodParser.Parse(period);
        var currentWeek = weekId ?? _currentWeek.GetCurrentWeekId();
        var (start, end) = PeriodRange.For(timePeriod, currentWeek, DateTime.UtcNow, year, month);
        var bytes = await _export.ExportAsync(groupId, start, end, cancellationToken);
        var stamp = start == end ? start.ToString("yyyy-MM-dd") : $"{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"board-{groupId}-{stamp}.xlsx");
    }
}
