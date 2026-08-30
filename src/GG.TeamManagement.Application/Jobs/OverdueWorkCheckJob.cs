using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Jobs;

public class OverdueWorkCheckJob
{
    private readonly IApplicationDbContext _db;

    public OverdueWorkCheckJob(IApplicationDbContext db)
    {
        _db = db;
    }

    public Task Execute() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));

        var overdue = await _db.WorkItems
            .Where(w => w.Deadline < cutoff && w.Status != WorkItemStatus.Done)
            .Select(w => new { w.Id, w.AssignedMemberId })
            .ToListAsync(cancellationToken);

        foreach (var item in overdue)
        {
            var exists = await _db.Notifications.AnyAsync(
                n => n.Type == NotificationType.WorkNotDoneTwoWeeks && n.WorkItemId == item.Id,
                cancellationToken);

            if (exists) continue;

            _db.Notifications.Add(new Notification
            {
                Type = NotificationType.WorkNotDoneTwoWeeks,
                WorkItemId = item.Id,
                MemberId = item.AssignedMemberId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
