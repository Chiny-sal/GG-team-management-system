using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Application.Members;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Deletions;

public class DeletionRequestService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly BoardService _boards;
    private readonly MemberService _members;

    public DeletionRequestService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        BoardService boards,
        MemberService members)
    {
        _db = db;
        _currentUser = currentUser;
        _boards = boards;
        _members = members;
    }

    public async Task<IReadOnlyList<DeletionRequestDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var pending = await _db.DeletionRequests.AsNoTracking()
            .Include(r => r.RequestedByMember)
            .Where(r => r.Status == DeletionRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
        return pending.Select(ToDto).ToList();
    }

    public Task<DeletionRequestDto> RequestGroupDeletionAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        RequestAsync(DeletionTargetType.Group, groupId, cancellationToken);

    public Task<DeletionRequestDto> RequestMemberDeletionAsync(Guid memberId, CancellationToken cancellationToken = default) =>
        RequestAsync(DeletionTargetType.Member, memberId, cancellationToken);

    public async Task ApproveAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        var actorId = RequireMemberId();

        var request = await _db.DeletionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Deletion request not found.");

        if (request.Status != DeletionRequestStatus.Pending)
            throw new InvalidOperationException("That deletion request is no longer pending.");

        if (request.RequestedByMemberId is Guid requesterId && requesterId == actorId)
            throw new InvalidOperationException("A different Office Management member must approve this deletion.");

        request.Status = DeletionRequestStatus.Approved;
        request.ResolvedByMemberId = actorId;
        request.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (request.TargetType == DeletionTargetType.Group)
        {
            await _boards.DeleteGroupAsync(request.TargetId, cancellationToken);
            request.GroupId = null;
        }
        else
            await _members.DeleteMemberAsync(request.TargetId, cancellationToken);

        _db.ActivityLogEntries.Add(new ActivityLogEntry
        {
            EntityType = request.TargetType == DeletionTargetType.Group ? "Group" : "Member",
            EntityId = request.TargetId,
            ChangeType = ChangeType.Deleted,
            Summary = request.TargetType == DeletionTargetType.Group
                ? $"Group '{request.TargetName}' was deleted."
                : $"Member '{request.TargetName}' was deleted.",
            ChangedByMemberId = actorId,
            GroupId = request.TargetType == DeletionTargetType.Group ? null : request.GroupId,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var request = await _db.DeletionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Deletion request not found.");

        if (request.Status != DeletionRequestStatus.Pending)
            throw new InvalidOperationException("That deletion request is no longer pending.");

        request.Status = DeletionRequestStatus.Cancelled;
        request.ResolvedByMemberId = RequireMemberId();
        request.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DeletionRequestDto> RequestAsync(
        DeletionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        var actorId = RequireMemberId();

        var existing = await _db.DeletionRequests.AsNoTracking()
            .Include(r => r.RequestedByMember)
            .FirstOrDefaultAsync(
                r => r.TargetType == targetType && r.TargetId == targetId && r.Status == DeletionRequestStatus.Pending,
                cancellationToken);
        if (existing is not null)
            return ToDto(existing);

        string targetName;
        Guid? groupId;

        if (targetType == DeletionTargetType.Group)
        {
            var group = await _db.Groups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == targetId, cancellationToken)
                ?? throw new KeyNotFoundException("Group not found.");
            if (group.IsOfficeManagementTeam)
                throw new InvalidOperationException("The Office Management group cannot be deleted.");
            targetName = group.Name;
            groupId = group.Id;
        }
        else
        {
            if (actorId == targetId)
                throw new InvalidOperationException("You cannot delete your own account.");

            var member = await _db.Members.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == targetId, cancellationToken)
                ?? throw new KeyNotFoundException("Member not found.");
            targetName = member.Name;
            groupId = member.GroupId;
        }

        var request = new DeletionRequest
        {
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            RequestedByMemberId = actorId,
            RequestedAt = DateTime.UtcNow,
            Status = DeletionRequestStatus.Pending,
            GroupId = groupId
        };
        _db.DeletionRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.DeletionRequests.AsNoTracking()
            .Include(r => r.RequestedByMember)
            .FirstAsync(r => r.Id == request.Id, cancellationToken);
        return ToDto(saved);
    }

    private async Task EnsureOfficeManagementAsync(CancellationToken cancellationToken)
    {
        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            throw new UnauthorizedAccessException("Only Office Management can manage deletion approvals.");
    }

    private Guid RequireMemberId() =>
        _currentUser.MemberId ?? throw new UnauthorizedAccessException("Current member is required.");

    private static DeletionRequestDto ToDto(DeletionRequest request) =>
        new(
            request.Id,
            request.TargetType,
            request.TargetId,
            request.TargetName,
            request.RequestedByMemberId,
            request.RequestedByMember?.Name,
            request.RequestedAt,
            request.Status,
            request.GroupId);
}
