using GG.TeamManagement.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WeekController : ControllerBase
{
    private readonly ICurrentWeekService _currentWeek;

    public WeekController(ICurrentWeekService currentWeek)
    {
        _currentWeek = currentWeek;
    }

    [HttpGet("current")]
    public ActionResult<object> Current() =>
        Ok(new { weekId = _currentWeek.GetCurrentWeekId() });
}
