using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Notifications;

public class NotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotificationService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<NotificationsPageDto> GetUnreadPageAsync(CancellationToken cancellationToken = default)
    {
        var isOffice = await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken);
        var canMarkRead = _currentUser.IsLead || isOffice;

        var query = _db.Notifications.AsNoTracking()
            .Include(n => n.Member)
            .Include(n => n.WorkItem)
            .Where(n => !n.IsRead);

        if (!isOffice)
        {
            var groupId = _currentUser.GroupId
                ?? throw new UnauthorizedAccessException("Current member is required.");

            query = query.Where(n =>
                (n.Member != null && n.Member.GroupId == groupId)
                || (n.WorkItem != null && n.WorkItem.GroupId == groupId));
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var since = AssignmentWindow.SinceUtc(DateTime.UtcNow);
        var memberIds = items
            .Where(n => n.Type == NotificationType.MemberNoAssignmentTwoWeeks && n.MemberId is not null)
            .Select(n => n.MemberId!.Value)
            .Distinct()
            .ToList();

        var recentlyAssigned = memberIds.Count == 0
            ? new HashSet<Guid>()
            : (await _db.WorkItems.AsNoTracking()
                .Where(w =>
                    w.AssignedMemberId != null
                    && memberIds.Contains(w.AssignedMemberId.Value)
                    && (w.AssignedAt ?? w.CreatedAt) >= since)
                .Select(w => w.AssignedMemberId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var groups = items
            .GroupBy(n => n.Type)
            .Select(g => new NotificationGroupDto(
                g.Key,
                g.Select(n => new NotificationDto(
                    n.Id,
                    n.Type,
                    n.MemberId,
                    n.Member?.Name,
                    n.WorkItemId,
                    n.WorkItem?.Title,
                    n.CreatedAt,
                    n.IsRead,
                    n.Type == NotificationType.MemberNoAssignmentTwoWeeks && n.MemberId is Guid memberId
                        ? recentlyAssigned.Contains(memberId)
                            ? AssignmentStatusLabel.AssignedWork
                            : AssignmentStatusLabel.NoAssignment
                        : null)).ToList()))
            .ToList();

        return new NotificationsPageDto(canMarkRead, groups);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageNotificationsAsync(cancellationToken))
            throw new UnauthorizedAccessException("Only Team Leads and Office Management can mark notifications as read.");

        var notification = await _db.Notifications
            .Include(n => n.Member)
            .Include(n => n.WorkItem)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Notification not found.");

        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
        {
            var groupId = _currentUser.GroupId
                ?? throw new UnauthorizedAccessException("Current member is required.");
            var belongsToGroup =
                (notification.Member != null && notification.Member.GroupId == groupId)
                || (notification.WorkItem != null && notification.WorkItem.GroupId == groupId);
            if (!belongsToGroup)
                throw new UnauthorizedAccessException("You can only mark notifications for your own group.");
        }

        if (notification.IsRead) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> CanManageNotificationsAsync(CancellationToken cancellationToken) =>
        _currentUser.IsLead || await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken);
}
