using GG.TeamManagement.Application.Abstractions;

namespace GG.TeamManagement.Application.Jobs;

public class CurrentWeekRefreshJob
{
    private readonly ICurrentWeekService _currentWeek;

    public CurrentWeekRefreshJob(ICurrentWeekService currentWeek)
    {
        _currentWeek = currentWeek;
    }

    public void Execute() => _currentWeek.Refresh();
}
