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
                .ThenInclude(m => m!.Group)
            .Include(n => n.WorkItem)
                .ThenInclude(w => w!.Group)
            .Where(n => !n.IsRead);

        if (!isOffice)
        {
            var groupId = _currentUser.GroupId
                ?? throw new UnauthorizedAccessException("Current member is required.");

            query = query.Where(n =>
                n.Type != NotificationType.MissedLastTwoMeetings
                && ((n.Member != null && n.Member.GroupId == groupId)
                    || (n.WorkItem != null && n.WorkItem.GroupId == groupId)));
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

        var stillMissing = await LoadMembersStillMissingLastTwoAsync(items, cancellationToken);

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
                    string.IsNullOrWhiteSpace(n.WorkItem?.Description) ? null : n.WorkItem.Description.Trim(),
                    n.WorkItem?.Deadline,
                    n.WorkItem?.Group?.Name ?? n.Member?.Group?.Name,
                    n.CreatedAt,
                    n.IsRead,
                    n.Type == NotificationType.MemberNoAssignmentTwoWeeks && n.MemberId is Guid memberId
                        ? recentlyAssigned.Contains(memberId)
                            ? AssignmentStatusLabel.AssignedWork
                            : AssignmentStatusLabel.NoAssignment
                        : null,
                    n.Type == NotificationType.MissedLastTwoMeetings && n.MemberId is Guid attendanceMemberId
                        ? stillMissing.Contains(attendanceMemberId)
                            ? AttendanceStatusLabel.MissedLastTwo
                            : AttendanceStatusLabel.AttendedSince
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

            if (notification.Type == NotificationType.MissedLastTwoMeetings)
                throw new UnauthorizedAccessException("Only Office Management can manage attendance notifications.");
        }

        if (notification.IsRead) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> CanManageNotificationsAsync(CancellationToken cancellationToken) =>
        _currentUser.IsLead || await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken);

    private async Task<HashSet<Guid>> LoadMembersStillMissingLastTwoAsync(
        IReadOnlyList<Domain.Entities.Notification> items,
        CancellationToken cancellationToken)
    {
        var attendanceMemberIds = items
            .Where(n => n.Type == NotificationType.MissedLastTwoMeetings && n.MemberId is not null)
            .Select(n => n.MemberId!.Value)
            .Distinct()
            .ToList();
        if (attendanceMemberIds.Count == 0)
            return [];

        var sessionIds = await _db.MeetingAttendances.AsNoTracking()
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .Take(2)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (sessionIds.Count < 2)
            return [];

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => sessionIds.Contains(r.MeetingAttendanceId) && attendanceMemberIds.Contains(r.MemberId))
            .Select(r => new { r.MeetingAttendanceId, r.MemberId, r.Present })
            .ToListAsync(cancellationToken);

        var stillMissing = new HashSet<Guid>();
        foreach (var memberId in attendanceMemberIds)
        {
            var missedBoth = sessionIds.All(sessionId =>
            {
                var record = records.FirstOrDefault(r =>
                    r.MeetingAttendanceId == sessionId && r.MemberId == memberId);
                return record is null || !record.Present;
            });
            if (missedBoth)
                stillMissing.Add(memberId);
        }

        return stillMissing;
    }
}
