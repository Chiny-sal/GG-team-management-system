using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Application.Telegram;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly BoardService _boards;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WorkItemTelegramService _telegram;

    public GroupsController(
        BoardService boards,
        UserManager<ApplicationUser> userManager,
        WorkItemTelegramService telegram)
    {
        _boards = boards;
        _userManager = userManager;
        _telegram = telegram;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _boards.GetGroupsAsync(cancellationToken));

    [HttpGet("{groupId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> Members(
        Guid groupId,
        CancellationToken cancellationToken) =>
        Ok(await _boards.GetMembersForAssignmentAsync(groupId, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.LeadOrOfficeManagement)]
    [HttpPost("{groupId:guid}/members")]
    public async Task<ActionResult<MemberDto>> AddMember(
        Guid groupId,
        [FromBody] AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Password is required.");

        var email = request.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("That email is already in use.");

        var member = await _boards.AddMemberAsync(groupId, request, cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MemberId = member.Id
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            await _boards.DeleteMemberAsync(member.Id, cancellationToken);
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Member");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            await _boards.DeleteMemberAsync(member.Id, cancellationToken);
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));
        }

        if (!string.IsNullOrWhiteSpace(member.TelegramUsername) || !string.IsNullOrWhiteSpace(member.TelegramUserId))
            await _telegram.NotifyAccountCreatedAsync(member.Id, email, request.Password, cancellationToken);

        return Ok(member);
    }

    [HttpPatch("{groupId:guid}")]
    public async Task<ActionResult<GroupDto>> Rename(
        Guid groupId,
        [FromBody] RenameGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _boards.RenameGroupAsync(groupId, request, cancellationToken);
        return Ok(group);
    }
}
