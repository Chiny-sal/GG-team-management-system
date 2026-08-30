using GG.TeamManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Activity;

public class ActivityService
{
    private readonly IApplicationDbContext _db;

    public ActivityService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<ActivityLogDto> Items, int Total)> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ActivityLogEntries.AsNoTracking()
            .Include(e => e.ChangedByMember)
            .OrderByDescending(e => e.OccurredAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ActivityLogDto(
                e.Id,
                e.EntityType,
                e.EntityId,
                e.ChangeType,
                e.Summary,
                e.ChangedByMemberId,
                e.ChangedByMember != null ? e.ChangedByMember.Name : null,
                e.OccurredAt))
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
