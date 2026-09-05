using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain;

public static class PeriodRange
{
    public static (DateOnly Start, DateOnly End) For(TimePeriod period, DateOnly currentWeek, DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);
        switch (period)
        {
            case TimePeriod.Month:
            {
                var first = new DateOnly(today.Year, today.Month, 1);
                var last = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                return (WeekId.FromDate(first), WeekId.FromDate(last));
            }
            case TimePeriod.Year:
            {
                var first = new DateOnly(today.Year, 1, 1);
                var last = new DateOnly(today.Year, 12, 31);
                return (WeekId.FromDate(first), WeekId.FromDate(last));
            }
            default:
                return (currentWeek, currentWeek);
        }
    }

    public static string Label(TimePeriod period, DateOnly currentWeek, DateTime utcNow) =>
        period switch
        {
            TimePeriod.Month => DateOnly.FromDateTime(utcNow).ToString("MMMM yyyy"),
            TimePeriod.Year => DateOnly.FromDateTime(utcNow).Year.ToString(),
            _ => $"Week of {currentWeek:yyyy-MM-dd}"
        };
}
