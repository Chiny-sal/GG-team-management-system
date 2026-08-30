using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Boards;

public class BoardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentWeekService _currentWeek;

    public BoardService(IApplicationDbContext db, ICurrentUser currentUser, ICurrentWeekService currentWeek)
    {
        _db = db;
        _currentUser = currentUser;
        _currentWeek = currentWeek;
    }

    public async Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Groups
            .AsNoTracking()
            .OrderByDescending(g => g.IsOfficeManagementTeam)
            .ThenBy(g => g.Name)
            .Select(g => new GroupDto(g.Id, g.Name, g.IsOfficeManagementTeam))
            .ToListAsync(cancellationToken);
    }

    public async Task<BoardDto> GetBoardAsync(Guid groupId, DateOnly? weekId, CancellationToken cancellationToken = default)
    {
        await EnsureCanViewGroupAsync(groupId, cancellationToken);

        var week = weekId ?? _currentWeek.GetCurrentWeekId();
        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new KeyNotFoundException("Group not found.");

        var members = await _db.Members.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Name)
            .Select(m => new MemberDto(m.Id, m.Name, m.GroupId, m.Role, m.TelegramUserId))
            .ToListAsync(cancellationToken);

        var items = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Where(w => w.GroupId == groupId && w.WeekId == week)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var snapshot = await _db.WeeklyBoardSnapshots.AsNoTracking()
            .Where(s => s.GroupId == groupId && s.WeekId == week)
            .OrderByDescending(s => s.SavedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new BoardDto(
            group.Id,
            group.Name,
            week,
            snapshot is not null,
            snapshot?.SavedAt,
            members,
            items.Select(WorkItemMapper.ToDto).ToList());
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(Guid groupId, CreateWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        EnsureLead();
        await EnsureBoardUnlockedAsync(groupId, _currentWeek.GetCurrentWeekId(), cancellationToken);

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
            Deadline = request.Deadline ?? week.AddDays(6),
            CreatedAt = DateTime.UtcNow,
            CreatedByMemberId = _currentUser.MemberId
                ?? throw new UnauthorizedAccessException("Current member is required.")
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
        await EnsureBoardUnlockedAsync(item.GroupId, item.WeekId, cancellationToken);

        if (_currentUser.IsLead)
        {
            if (request.Title is not null) item.Title = request.Title.Trim();
            if (request.Description is not null) item.Description = request.Description.Trim();
            if (request.Deadline is not null) item.Deadline = request.Deadline.Value;
            if (request.ClearAssignment)
            {
                item.AssignedMemberId = null;
                item.Status = WorkItemStatus.NotAssigned;
            }
            else if (request.AssignedMemberId is not null)
            {
                await EnsureMemberInGroupAsync(request.AssignedMemberId.Value, item.GroupId, cancellationToken);
                item.AssignedMemberId = request.AssignedMemberId;
                if (item.Status == WorkItemStatus.NotAssigned)
                    item.Status = WorkItemStatus.Assigned;
            }

            if (request.Status is not null)
            {
                item.Status = request.Status.Value;
                if (item.Status == WorkItemStatus.NotAssigned)
                    item.AssignedMemberId = null;
                else if (item.AssignedMemberId is null)
                    throw new InvalidOperationException("Assign a member before changing status away from NotAssigned.");
            }
        }
        else
        {
            if (item.AssignedMemberId != _currentUser.MemberId)
                throw new UnauthorizedAccessException("Members can only move their own work items.");

            if (request.AssignedMemberId is not null && request.AssignedMemberId != item.AssignedMemberId)
                throw new UnauthorizedAccessException("Members cannot reassign work to other members.");

            if (request.ClearAssignment)
                throw new UnauthorizedAccessException("Members cannot unassign work.");

            if (request.Title is not null || request.Description is not null || request.Deadline is not null)
                throw new UnauthorizedAccessException("Members can only change the status of their own cards.");

            if (request.Status is null)
                throw new InvalidOperationException("Status is required.");

            if (request.Status is WorkItemStatus.NotAssigned)
                throw new UnauthorizedAccessException("Members cannot unassign work.");

            item.Status = request.Status.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        var updated = await _db.WorkItems
            .Include(w => w.AssignedMember)
            .FirstAsync(w => w.Id == item.Id, cancellationToken);
        return WorkItemMapper.ToDto(updated);
    }

    public async Task<SaveBoardResponse> SaveBoardAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        EnsureLead();
        var week = _currentWeek.GetCurrentWeekId();
        await EnsureBoardUnlockedAsync(groupId, week, cancellationToken);

        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");

        var snapshot = new WeeklyBoardSnapshot
        {
            GroupId = groupId,
            WeekId = week,
            SavedAt = DateTime.UtcNow,
            SavedByMemberId = _currentUser.MemberId
                ?? throw new UnauthorizedAccessException("Current member is required.")
        };

        _db.WeeklyBoardSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);
        return new SaveBoardResponse(snapshot.Id, snapshot.SavedAt, snapshot.WeekId);
    }

    private void EnsureLead()
    {
        if (!_currentUser.IsLead)
            throw new UnauthorizedAccessException("Only leads can perform this action.");
    }

    private async Task EnsureCanViewGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsLead) return;
        if (_currentUser.GroupId != groupId)
            throw new UnauthorizedAccessException("Members can only view their own group board.");

        if (!await _db.Groups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new KeyNotFoundException("Group not found.");
    }

    private async Task EnsureBoardUnlockedAsync(Guid groupId, DateOnly weekId, CancellationToken cancellationToken)
    {
        var locked = await _db.WeeklyBoardSnapshots
            .AnyAsync(s => s.GroupId == groupId && s.WeekId == weekId, cancellationToken);
        if (locked)
            throw new InvalidOperationException("This week's board is locked.");
    }

    private async Task EnsureMemberInGroupAsync(Guid memberId, Guid groupId, CancellationToken cancellationToken)
    {
        var exists = await _db.Members.AnyAsync(m => m.Id == memberId && m.GroupId == groupId, cancellationToken);
        if (!exists)
            throw new InvalidOperationException("Assigned member must belong to the same group.");
    }
}
