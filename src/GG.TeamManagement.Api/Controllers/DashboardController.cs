using GG.TeamManagement.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] string? period,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetAsync(period, year, month, cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPost("suggestions")]
    public async Task<ActionResult<TopicSuggestionDto>> AddSuggestion(
        [FromBody] AddTopicSuggestionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _dashboard.AddSuggestionAsync(request, cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPost("suggestions/{id:guid}/promote")]
    public async Task<ActionResult<MeetingDto>> Promote(Guid id, CancellationToken cancellationToken) =>
        Ok(await _dashboard.PromoteSuggestionAsync(id, cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPatch("meetings/{id:guid}/notes")]
    public async Task<ActionResult<MeetingDto>> UpdateNotes(
        Guid id,
        [FromBody] UpdateMeetingNotesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _dashboard.UpdateNotesAsync(id, request, cancellationToken));
}
