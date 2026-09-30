using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Attendance;
using GG.TeamManagement.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.OfficeManagementOnly)]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendance;
    private readonly IAttendanceExportService _export;

    public AttendanceController(AttendanceService attendance, IAttendanceExportService export)
    {
        _attendance = attendance;
        _export = export;
    }

    [HttpGet("roster")]
    public async Task<ActionResult<IReadOnlyList<AttendanceRosterMemberDto>>> Roster(
        CancellationToken cancellationToken) =>
        Ok(await _attendance.GetRosterAsync(cancellationToken));

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AttendanceSessionSummaryDto>>> Sessions(
        CancellationToken cancellationToken) =>
        Ok(await _attendance.ListSessionsAsync(cancellationToken));

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<AttendanceSessionDto>> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _attendance.GetSessionAsync(id, cancellationToken));

    [HttpPost("sessions")]
    public async Task<ActionResult<AttendanceSessionDto>> Create(
        [FromBody] SaveAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _attendance.CreateSessionAsync(request, cancellationToken);
        return Ok(session);
    }

    [HttpPut("sessions/{id:guid}")]
    public async Task<ActionResult<AttendanceSessionDto>> Update(
        Guid id,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _attendance.UpdateSessionAsync(id, request, cancellationToken));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var bytes = await _export.ExportAsync(cancellationToken);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "attendance-history.xlsx");
    }

    [HttpPost("broadcast")]
    public async Task<ActionResult<BroadcastMessageResult>> Broadcast(
        [FromBody] BroadcastMessageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _attendance.BroadcastAsync(request, cancellationToken));
}
