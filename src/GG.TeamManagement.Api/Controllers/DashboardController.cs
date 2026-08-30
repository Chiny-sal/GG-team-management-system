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
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetAsync(cancellationToken));

    [Authorize(Roles = "Lead")]
    [HttpPost("suggestions/{id:guid}/promote")]
    public async Task<ActionResult<MeetingDto>> Promote(Guid id, CancellationToken cancellationToken) =>
        Ok(await _dashboard.PromoteSuggestionAsync(id, cancellationToken));
}
