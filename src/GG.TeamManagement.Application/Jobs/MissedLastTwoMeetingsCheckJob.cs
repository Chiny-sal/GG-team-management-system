using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Jobs;

public class MissedLastTwoMeetingsCheckJob
{
    private readonly IApplicationDbContext _db;

    public MissedLastTwoMeetingsCheckJob(IApplicationDbContext db)
    {
        _db = db;
    }

    public Task Execute() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessionIds = await _db.MeetingAttendances.AsNoTracking()
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .Take(2)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var unread = await _db.Notifications
            .Where(n => n.Type == NotificationType.MissedLastTwoMeetings && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (sessionIds.Count < 2)
        {
            foreach (var note in unread)
                note.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => sessionIds.Contains(r.MeetingAttendanceId))
            .Select(r => new { r.MeetingAttendanceId, r.MemberId, r.Present })
            .ToListAsync(cancellationToken);

        var memberIds = await _db.Members.AsNoTracking()
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var missedIds = new HashSet<Guid>();
        foreach (var memberId in memberIds)
        {
            var missedBoth = sessionIds.All(sessionId =>
            {
                var record = records.FirstOrDefault(r =>
                    r.MeetingAttendanceId == sessionId && r.MemberId == memberId);
                return record is null || !record.Present;
            });
            if (missedBoth)
                missedIds.Add(memberId);
        }

        foreach (var note in unread)
        {
            if (note.MemberId is not Guid memberId || !missedIds.Contains(memberId))
                note.IsRead = true;
        }

        var alreadyUnread = unread
            .Where(n => !n.IsRead && n.MemberId is not null)
            .Select(n => n.MemberId!.Value)
            .ToHashSet();

        foreach (var memberId in missedIds)
        {
            if (alreadyUnread.Contains(memberId)) continue;

            _db.Notifications.Add(new Notification
            {
                Type = NotificationType.MissedLastTwoMeetings,
                MemberId = memberId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
