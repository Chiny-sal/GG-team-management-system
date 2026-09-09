using GG.TeamManagement.Application.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly ActivityService _activity;

    public ActivityController(ActivityService activity)
    {
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _activity.GetAsync(page, pageSize, q, cancellationToken);
        return Ok(new { items, total, page, pageSize });
    }
}
