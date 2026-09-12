using GG.TeamManagement.Application.Members;
using GG.TeamManagement.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/members")]
public class MembersController : ControllerBase
{
    private readonly MemberService _members;
    private readonly IMemberWorkExportService _export;

    public MembersController(MemberService members, IMemberWorkExportService export)
    {
        _members = members;
        _export = export;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberWorkSummaryDto>>> Directory(CancellationToken cancellationToken) =>
        Ok(await _members.GetDirectoryAsync(cancellationToken));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        await _members.EnsureCanViewDirectoryAsync(cancellationToken);
        var bytes = await _export.ExportAsync(cancellationToken);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "members-work-summary.xlsx");
    }

    [HttpPost("set-leads")]
    public async Task<IActionResult> SetLeads(
        [FromBody] SetLeadsRequest request,
        CancellationToken cancellationToken)
    {
        await _members.SetAsLeadsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{memberId:guid}/revoke-lead")]
    public async Task<IActionResult> RevokeLead(Guid memberId, CancellationToken cancellationToken)
    {
        await _members.RevokeLeadAsync(memberId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{memberId:guid}/cross-group-boards")]
    public async Task<ActionResult<MemberProfileDto>> SetCrossGroupBoardAccess(
        Guid memberId,
        [FromBody] SetCrossGroupBoardAccessRequest request,
        CancellationToken cancellationToken)
    {
        await _members.SetCrossGroupBoardAccessAsync(memberId, request, cancellationToken);
        return Ok(await _members.GetProfileAsync(memberId, cancellationToken));
    }

    [HttpPatch("{memberId:guid}/cross-group-assignment")]
    public async Task<ActionResult<MemberProfileDto>> SetCrossGroupAssignmentAccess(
        Guid memberId,
        [FromBody] SetCrossGroupAssignmentAccessRequest request,
        CancellationToken cancellationToken)
    {
        await _members.SetCrossGroupAssignmentAccessAsync(memberId, request, cancellationToken);
        return Ok(await _members.GetProfileAsync(memberId, cancellationToken));
    }

    [HttpGet("{memberId:guid}")]
    public async Task<ActionResult<MemberProfileDto>> Get(Guid memberId, CancellationToken cancellationToken) =>
        Ok(await _members.GetProfileAsync(memberId, cancellationToken));

    [HttpPatch("{memberId:guid}")]
    public async Task<ActionResult<MemberProfileDto>> Update(
        Guid memberId,
        [FromBody] UpdateMemberProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _members.UpdateProfileAsync(memberId, request, cancellationToken));

    [HttpPost("{memberId:guid}/password")]
    public async Task<IActionResult> ChangePassword(
        Guid memberId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _members.ChangePasswordAsync(memberId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{memberId:guid}")]
    public async Task<IActionResult> Delete(Guid memberId, CancellationToken cancellationToken)
    {
        await _members.DeleteMemberAsync(memberId, cancellationToken);
        return NoContent();
    }
}
