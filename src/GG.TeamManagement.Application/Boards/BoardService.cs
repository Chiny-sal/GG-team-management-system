using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Application.Telegram;
using GG.TeamManagement.Domain;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Boards;

public class BoardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentWeekService _currentWeek;
    private readonly WorkItemTelegramService _telegram;

    private Member? _currentMember;
    private bool _currentMemberLoaded;

    public BoardService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        ICurrentWeekService currentWeek,
        WorkItemTelegramService telegram)
    {
        _db = db;
        _currentUser = currentUser;
        _currentWeek = currentWeek;
        _telegram = telegram;
    }

    public async Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var query = _db.Groups.AsNoTracking().AsQueryable();
        if (!await CanListAllGroupsAsync(cancellationToken) && _currentUser.GroupId is Guid groupId)
            query = query.Where(g => g.Id == groupId);

        return await query
            .OrderByDescending(g => g.IsOfficeManagementTeam)
            .ThenBy(g => g.Name)
            .Select(g => new GroupDto(g.Id, g.Name, g.IsOfficeManagementTeam))
            .ToListAsync(cancellationToken);
    }

    public async Task<BoardDto> GetBoardAsync(
        Guid groupId,
        DateOnly? weekId,
        string? period,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewGroupAsync(groupId, cancellationToken);

        var timePeriod = TimePeriodParser.Parse(period);
        var currentWeek = weekId ?? _currentWeek.GetCurrentWeekId();
        var utcNow = DateTime.UtcNow;
        var (rangeStart, rangeEnd) = PeriodRange.For(timePeriod, currentWeek, utcNow, year, month);

        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new KeyNotFoundException("Group not found.");

        var members = await _db.Members.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Name)
            .Select(m => new MemberDto(m.Id, m.Name, m.GroupId, m.Role, m.TelegramUserId, m.TelegramUsername))
            .ToListAsync(cancellationToken);

        var items = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Where(w => w.GroupId == groupId && w.WeekId >= rangeStart && w.WeekId <= rangeEnd)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var meetings = await _db.Meetings.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.ScheduledDate)
            .Select(m => new MeetingSummaryDto(m.Id, m.ScheduledDate, m.TopicText))
            .ToListAsync(cancellationToken);

        return new BoardDto(
            group.Id,
            group.Name,
            currentWeek,
            TimePeriodParser.ToQuery(timePeriod),
            PeriodRange.Label(timePeriod, currentWeek, utcNow, year, month),
            rangeStart,
            rangeEnd,
            false,
            null,
            [],
            members,
            items.Select(WorkItemMapper.ToDto).ToList(),
            meetings);
    }

    public async Task<WorkItemDetailDto> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var item = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Include(w => w.CreatedByMember)
            .Include(w => w.Group)
            .Include(w => w.Meeting)
            .FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Work item not found.");

        await EnsureCanViewGroupAsync(item.GroupId, cancellationToken);
        return WorkItemMapper.ToDetailDto(item);
    }

    public async Task<WorkRegistryDto> GetRegistryAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await EnsureCanViewGroupAsync(groupId, cancellationToken);

        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new KeyNotFoundException("Group not found.");

        var items = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Where(w => w.GroupId == groupId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return new WorkRegistryDto(group.Id, group.Name, items.Select(WorkItemMapper.ToDto).ToList());
    }

    public async Task<IReadOnlyList<MemberDto>> GetMembersForAssignmentAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanListMembersForAssignmentAsync(groupId, cancellationToken);

        return await _db.Members.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Name)
            .Select(m => new MemberDto(m.Id, m.Name, m.GroupId, m.Role, m.TelegramUserId, m.TelegramUsername))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(Guid groupId, CreateWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCanCreateWorkOnGroupAsync(groupId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Title is required.");

        var week = _currentWeek.GetCurrentWeekId();
        var item = new WorkItem
        {
            GroupId = groupId,
            WeekId = week,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = WorkItemStatus.NotAssigned,
            Deadline = request.Deadline,
            CreatedAt = DateTime.UtcNow,
            CreatedByMemberId = _currentUser.MemberId
                ?? throw new UnauthorizedAccessException("Current member is required."),
            MeetingId = await ResolveMeetingIdAsync(request.MeetingId, cancellationToken)
        };

        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return WorkItemMapper.ToDto(item);
    }

    public async Task<WorkItemDto> UpdateWorkItemAsync(Guid workItemId, UpdateWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _db.WorkItems
            .Include(w => w.AssignedMember)
            .FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Work item not found.");

        await EnsureCanViewGroupAsync(item.GroupId, cancellationToken);
        var canFullyEdit = await CanManageWorkOnGroupAsync(item.GroupId, cancellationToken);
        if (request.GroupId is Guid targetGroup && targetGroup != item.GroupId)
        {
            await EnsureCanCreateWorkOnGroupAsync(targetGroup, cancellationToken);
            item.GroupId = targetGroup;
            canFullyEdit = true;
        }
        if (request.AssignedMemberId is not null)
            await EnsureAssigneeAllowedAsync(request.AssignedMemberId.Value, item, cancellationToken);
        if (request.MeetingId is not null)
            await EnsureMeetingExistsAsync(request.MeetingId.Value, cancellationToken);
        var previousAssignee = item.AssignedMemberId;
        ApplyWorkItemUpdate(item, request, canFullyEdit);
        await _db.SaveChangesAsync(cancellationToken);

        if (item.AssignedMemberId is Guid assignee && assignee != previousAssignee)
            await _telegram.NotifyAssignmentAsync(item.Id, cancellationToken);

        var updated = await _db.WorkItems
            .Include(w => w.AssignedMember)
            .FirstAsync(w => w.Id == item.Id, cancellationToken);
        return WorkItemMapper.ToDto(updated);
    }

    public async Task CommitBoardAsync(Guid groupId, CommitBoardRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCanCommitToGroupAsync(groupId, cancellationToken);
        var hasMemberUpdates = request.MemberUpdates is { Count: > 0 };
        var hasWorkItems = request.WorkItems is { Count: > 0 };
        if (!hasMemberUpdates && !hasWorkItems)
            return;

        await _db.Groups.Where(g => g.Id == groupId).ToListAsync(cancellationToken);
        await _db.Members.Where(m => m.GroupId == groupId).ToListAsync(cancellationToken);

        var week = _currentWeek.GetCurrentWeekId();
        var newlyAssignedIds = new List<Guid>();
        var newUnassignedTitles = new List<string>();

        if (request.MemberUpdates is { Count: > 0 })
        {
            await EnsureLeadOfGroupAsync(groupId, cancellationToken);
            foreach (var update in request.MemberUpdates)
            {
                var member = await _db.Members
                    .FirstOrDefaultAsync(m => m.Id == update.Id && m.GroupId == groupId, cancellationToken)
                    ?? throw new KeyNotFoundException("Member not found.");

                var name = update.Name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Member name is required.");

                member.Name = name;
            }
        }

        var canFullyEdit = await CanManageWorkOnGroupAsync(groupId, cancellationToken);
        var canAssignCrossGroup = await CanAssignWorkToOtherGroupsAsync(cancellationToken);
        foreach (var change in request.WorkItems ?? [])
        {
            if (change.IsNew)
            {
                var targetGroupId = change.GroupId is Guid gid && gid != Guid.Empty ? gid : groupId;
                await EnsureCanCreateWorkOnGroupAsync(targetGroupId, cancellationToken);
                if (string.IsNullOrWhiteSpace(change.Title))
                    throw new InvalidOperationException("Title is required.");

                if (change.AssignedMemberId is not null)
                    await EnsureMemberInGroupAsync(change.AssignedMemberId.Value, targetGroupId, cancellationToken);

                var status = change.AssignedMemberId is null ? WorkItemStatus.NotAssigned : change.Status;
                if (status != WorkItemStatus.NotAssigned && change.AssignedMemberId is null)
                    throw new InvalidOperationException("Assign a member before changing status away from NotAssigned.");

                var itemId = change.Id == Guid.Empty ? Guid.NewGuid() : change.Id;
                var createdAt = DateTime.UtcNow;
                _db.WorkItems.Add(new WorkItem
                {
                    Id = itemId,
                    GroupId = targetGroupId,
                    WeekId = week,
                    Title = change.Title.Trim(),
                    Description = change.Description?.Trim() ?? string.Empty,
                    AssignedMemberId = change.AssignedMemberId,
                    AssignedAt = change.AssignedMemberId is not null ? createdAt : null,
                    Status = status,
                    Deadline = change.Deadline,
                    CreatedAt = createdAt,
                    CreatedByMemberId = _currentUser.MemberId
                        ?? throw new UnauthorizedAccessException("Current member is required."),
                    MeetingId = await ResolveMeetingIdAsync(change.MeetingId, cancellationToken)
                });

                if (change.AssignedMemberId is not null)
                    newlyAssignedIds.Add(itemId);
                else
                    newUnassignedTitles.Add(change.Title.Trim());
                continue;
            }

            var item = await _db.WorkItems
                .FirstOrDefaultAsync(w => w.Id == change.Id && w.GroupId == groupId, cancellationToken)
                ?? throw new KeyNotFoundException("Work item not found.");

            var previousAssignee = item.AssignedMemberId;
            var destinationGroupId = change.GroupId is Guid movedGroupId && movedGroupId != Guid.Empty
                ? movedGroupId
                : item.GroupId;
            var movingGroups = destinationGroupId != item.GroupId;
            var canEditThisItem = canFullyEdit || (canAssignCrossGroup && movingGroups);

            if (canEditThisItem)
            {
                if (movingGroups)
                {
                    await EnsureCanCreateWorkOnGroupAsync(destinationGroupId, cancellationToken);
                    item.GroupId = destinationGroupId;
                }

                if (change.AssignedMemberId is not null)
                    await EnsureAssigneeAllowedAsync(change.AssignedMemberId.Value, item, cancellationToken);
                if (change.MeetingId is not null)
                    await EnsureMeetingExistsAsync(change.MeetingId.Value, cancellationToken);

                ApplyWorkItemUpdate(item, new UpdateWorkItemRequest(
                    change.Title,
                    change.Description ?? string.Empty,
                    change.AssignedMemberId,
                    change.AssignedMemberId is null,
                    change.Status,
                    change.Deadline,
                    change.Deadline is null,
                    change.MeetingId,
                    change.MeetingId is null,
                    destinationGroupId),
                    canFullyEdit: true);
            }
            else
            {
                ApplyWorkItemUpdate(item, new UpdateWorkItemRequest(
                    null,
                    null,
                    null,
                    false,
                    change.Status,
                    null),
                    canFullyEdit: false);
            }

            if (item.AssignedMemberId is Guid assignee && assignee != previousAssignee)
                newlyAssignedIds.Add(item.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var workItemId in newlyAssignedIds)
            await _telegram.NotifyAssignmentAsync(workItemId, cancellationToken);

        foreach (var title in newUnassignedTitles)
            await _telegram.NotifyUnassignedToLeadsAsync(groupId, title, cancellationToken);
    }

    public async Task<MemberDto> AddMemberAsync(Guid groupId, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureLeadOfGroupAsync(groupId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");

        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        await _db.Groups.Where(g => g.Id == groupId).ToListAsync(cancellationToken);

        var username = TelegramHandle.Normalize(request.TelegramUsername);
        var telegramUserId = string.IsNullOrWhiteSpace(request.TelegramUserId)
            ? null
            : request.TelegramUserId.Trim();
        if (telegramUserId is null && TelegramHandle.LooksLikeNumericId(username))
            telegramUserId = username;

        var member = new Member
        {
            Name = request.Name.Trim(),
            GroupId = groupId,
            Role = MemberRole.Member,
            TelegramUserId = telegramUserId,
            TelegramUsername = username
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync(cancellationToken);
        return new MemberDto(member.Id, member.Name, member.GroupId, member.Role, member.TelegramUserId, member.TelegramUsername);
    }

    public async Task<GroupDto> RenameGroupAsync(Guid groupId, RenameGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            throw new UnauthorizedAccessException("Only Office Management can rename groups.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Group name is required.");
        if (name.Length > 200)
            throw new InvalidOperationException("Group name must be 200 characters or fewer.");

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new KeyNotFoundException("Group not found.");

        group.Name = name;
        await _db.SaveChangesAsync(cancellationToken);
        return new GroupDto(group.Id, group.Name, group.IsOfficeManagementTeam);
    }

    public async Task DeleteMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null) return;
        await EnsureLeadOfGroupAsync(member.GroupId, cancellationToken);
        _db.Members.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void ApplyWorkItemUpdate(WorkItem item, UpdateWorkItemRequest request, bool canFullyEdit)
    {
        if (canFullyEdit)
        {
            if (request.Title is not null) item.Title = request.Title.Trim();
            if (request.Description is not null) item.Description = request.Description.Trim();
            if (request.ClearDeadline) item.Deadline = null;
            else if (request.Deadline is not null) item.Deadline = request.Deadline.Value;

            if (request.ClearMeeting) item.MeetingId = null;
            else if (request.MeetingId is not null) item.MeetingId = request.MeetingId;

            if (request.ClearAssignment)
            {
                item.AssignedMemberId = null;
                item.AssignedAt = null;
                item.Status = WorkItemStatus.NotAssigned;
            }
            else if (request.AssignedMemberId is not null)
            {
                if (item.AssignedMemberId != request.AssignedMemberId)
                    item.AssignedAt = DateTime.UtcNow;
                item.AssignedMemberId = request.AssignedMemberId;
                if (item.Status == WorkItemStatus.NotAssigned)
                    item.Status = WorkItemStatus.Assigned;
            }

            if (request.Status is not null)
            {
                item.Status = request.Status.Value;
                if (item.Status == WorkItemStatus.NotAssigned)
                {
                    item.AssignedMemberId = null;
                    item.AssignedAt = null;
                }
                else if (item.AssignedMemberId is null)
                    throw new InvalidOperationException("Assign a member before changing status away from NotAssigned.");
            }
        }
        else
        {
            if (_currentUser.GroupId != item.GroupId)
                throw new UnauthorizedAccessException("You can only view this group's board.");

            if (item.AssignedMemberId != _currentUser.MemberId)
                throw new UnauthorizedAccessException("Members can only move their own work items.");

            if (request.AssignedMemberId is not null && request.AssignedMemberId != item.AssignedMemberId)
                throw new UnauthorizedAccessException("Members cannot reassign work to other members.");

            if (request.ClearAssignment)
                throw new UnauthorizedAccessException("Members cannot unassign work.");

            if (request.Title is not null || request.Description is not null || request.Deadline is not null || request.ClearDeadline)
                throw new UnauthorizedAccessException("Members can only change the status of their own cards.");

            if (request.Status is null)
                throw new InvalidOperationException("Status is required.");

            if (request.Status is WorkItemStatus.NotAssigned)
                throw new UnauthorizedAccessException("Members cannot unassign work.");

            item.Status = request.Status.Value;
        }
    }

    private async Task EnsureMeetingExistsAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        if (!await _db.Meetings.AnyAsync(m => m.Id == meetingId, cancellationToken))
            throw new KeyNotFoundException("Meeting not found.");
    }

    private async Task<Guid?> ResolveMeetingIdAsync(Guid? meetingId, CancellationToken cancellationToken)
    {
        if (meetingId is null) return null;
        await EnsureMeetingExistsAsync(meetingId.Value, cancellationToken);
        return meetingId;
    }

    private async Task<Member?> CurrentMemberAsync(CancellationToken cancellationToken)
    {
        if (_currentMemberLoaded)
            return _currentMember;

        _currentMemberLoaded = true;
        if (_currentUser.MemberId is not Guid memberId)
            return null;

        _currentMember = await _db.Members.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        return _currentMember;
    }

    private async Task<bool> CanViewAllBoardsAsync(CancellationToken cancellationToken)
    {
        if (await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            return true;

        var member = await CurrentMemberAsync(cancellationToken);
        return member?.CanViewOtherGroupBoards == true;
    }

    private async Task<bool> CanAssignWorkToOtherGroupsAsync(CancellationToken cancellationToken)
    {
        if (await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            return true;

        var member = await CurrentMemberAsync(cancellationToken);
        return member?.CanAssignWorkToOtherGroups == true;
    }

    private async Task<bool> CanListAllGroupsAsync(CancellationToken cancellationToken) =>
        await CanViewAllBoardsAsync(cancellationToken) || await CanAssignWorkToOtherGroupsAsync(cancellationToken);

    private async Task<bool> CanManageWorkOnGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (await IsLeadOfGroupAsync(groupId, cancellationToken))
            return true;

        var member = await CurrentMemberAsync(cancellationToken);
        return member?.CanAssignWorkToOtherGroups == true && member.GroupId != groupId;
    }

    private async Task EnsureCanCreateWorkOnGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        if (await IsLeadOfGroupAsync(groupId, cancellationToken))
            return;

        if (_currentUser.GroupId != groupId && await CanAssignWorkToOtherGroupsAsync(cancellationToken))
            return;

        if (_currentUser.GroupId != groupId)
            throw new UnauthorizedAccessException("You do not have permission to assign work to other groups.");

        throw new UnauthorizedAccessException("Only leads of this group or Office Management can perform this action.");
    }

    private async Task EnsureCanCommitToGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        if (_currentUser.GroupId == groupId)
            return;

        if (await CanViewAllBoardsAsync(cancellationToken) || await CanAssignWorkToOtherGroupsAsync(cancellationToken))
            return;

        throw new UnauthorizedAccessException("Members can only view their own group board.");
    }

    private async Task EnsureCanListMembersForAssignmentAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        if (_currentUser.GroupId == groupId)
            return;

        if (await CanListAllGroupsAsync(cancellationToken))
            return;

        throw new UnauthorizedAccessException("You do not have permission to assign work to other groups.");
    }

    private async Task EnsureAssigneeAllowedAsync(Guid memberId, WorkItem item, CancellationToken cancellationToken)
    {
        var assignee = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new InvalidOperationException("Assigned member was not found.");

        if (assignee.GroupId == item.GroupId)
            return;

        if (!await CanAssignWorkToOtherGroupsAsync(cancellationToken))
            throw new UnauthorizedAccessException("You do not have permission to assign work to other groups.");

        item.GroupId = assignee.GroupId;
    }

    private async Task<bool> IsLeadOfGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            return true;

        var member = await CurrentMemberAsync(cancellationToken);
        return member is { Role: MemberRole.Lead } && member.GroupId == groupId;
    }

    private async Task EnsureLeadOfGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (!await IsLeadOfGroupAsync(groupId, cancellationToken))
            throw new UnauthorizedAccessException("Only leads of this group or Office Management can perform this action.");
    }

    private async Task EnsureCanViewGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        if (_currentUser.GroupId == groupId)
            return;

        if (await CanViewAllBoardsAsync(cancellationToken))
            return;

        throw new UnauthorizedAccessException("Members can only view their own group board.");
    }

    private async Task EnsureMemberInGroupAsync(Guid memberId, Guid groupId, CancellationToken cancellationToken)
    {
        var exists = await _db.Members.AnyAsync(m => m.Id == memberId && m.GroupId == groupId, cancellationToken);
        if (!exists)
            throw new InvalidOperationException("Assigned member must belong to the same group.");
    }
}
