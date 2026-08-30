using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Jobs;

public class MemberAssignmentCheckJob
{
    private readonly IApplicationDbContext _db;

    public MemberAssignmentCheckJob(IApplicationDbContext db)
    {
        _db = db;
    }

    public Task Execute() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-14);
        var weekStart = WeekId.CurrentUtc().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var members = await _db.Members.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            var hasAssignment = await _db.WorkItems.AnyAsync(
                w => w.AssignedMemberId == member.Id && w.CreatedAt >= since,
                cancellationToken);

            if (hasAssignment) continue;

            var existsThisWeek = await _db.Notifications.AnyAsync(
                n => n.Type == NotificationType.MemberNoAssignmentTwoWeeks
                     && n.MemberId == member.Id
                     && n.CreatedAt >= weekStart,
                cancellationToken);

            if (existsThisWeek) continue;

            _db.Notifications.Add(new Notification
            {
                Type = NotificationType.MemberNoAssignmentTwoWeeks,
                MemberId = member.Id,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
