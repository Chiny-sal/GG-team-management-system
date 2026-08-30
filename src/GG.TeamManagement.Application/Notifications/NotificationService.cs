using GG.TeamManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Notifications;

public class NotificationService
{
    private readonly IApplicationDbContext _db;

    public NotificationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationGroupDto>> GetUnreadGroupedAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.Notifications.AsNoTracking()
            .Include(n => n.Member)
            .Include(n => n.WorkItem)
            .Where(n => !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return items
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
                    n.IsRead)).ToList()))
            .ToList();
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Notification not found.");

        if (notification.IsRead) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
