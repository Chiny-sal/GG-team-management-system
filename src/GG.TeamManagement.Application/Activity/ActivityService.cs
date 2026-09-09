using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Activity;

public class ActivityService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ActivityService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<(IReadOnlyList<ActivityLogDto> Items, int Total)> GetAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ActivityLogEntries.AsNoTracking()
            .Include(e => e.ChangedByMember)
            .AsQueryable();

        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
        {
            var groupId = _currentUser.GroupId
                ?? throw new UnauthorizedAccessException("Current member is required.");
            query = query.Where(e => e.GroupId == groupId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(e => e.Summary.ToLower().Contains(needle.ToLower()));
        }

        query = query.OrderByDescending(e => e.OccurredAt);

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
