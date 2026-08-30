using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace GG.TeamManagement.Infrastructure.Week;

public class CurrentWeekService : ICurrentWeekService
{
    public const string CacheKey = "CurrentWeekId";
    private readonly IMemoryCache _cache;

    public CurrentWeekService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public DateOnly GetCurrentWeekId()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(8);
            return WeekId.CurrentUtc();
        });
    }

    public DateOnly Refresh()
    {
        var weekId = WeekId.CurrentUtc();
        _cache.Set(CacheKey, weekId, TimeSpan.FromDays(8));
        return weekId;
    }
}
