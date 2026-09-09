using System.Globalization;
using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain;

public static class PeriodRange
{
    public static (DateOnly Start, DateOnly End) For(
        TimePeriod period,
        DateOnly currentWeek,
        DateTime utcNow,
        int? year = null,
        int? month = null)
    {
        var today = DateOnly.FromDateTime(utcNow);
        switch (period)
        {
            case TimePeriod.Month:
            {
                var selectedYear = ClampYear(year, today.Year);
                var selectedMonth = ClampMonth(month, today.Month);
                var first = new DateOnly(selectedYear, selectedMonth, 1);
                var last = new DateOnly(selectedYear, selectedMonth, DateTime.DaysInMonth(selectedYear, selectedMonth));
                return (WeekId.FromDate(first), WeekId.FromDate(last));
            }
            case TimePeriod.Year:
            {
                var selectedYear = ClampYear(year, today.Year);
                var first = new DateOnly(selectedYear, 1, 1);
                var last = new DateOnly(selectedYear, 12, 31);
                return (WeekId.FromDate(first), WeekId.FromDate(last));
            }
            default:
                return (currentWeek, currentWeek);
        }
    }

    public static string Label(
        TimePeriod period,
        DateOnly currentWeek,
        DateTime utcNow,
        int? year = null,
        int? month = null)
    {
        var today = DateOnly.FromDateTime(utcNow);
        return period switch
        {
            TimePeriod.Month => new DateOnly(ClampYear(year, today.Year), ClampMonth(month, today.Month), 1)
                .ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            TimePeriod.Year => ClampYear(year, today.Year).ToString(CultureInfo.InvariantCulture),
            _ => $"Week of {currentWeek:yyyy-MM-dd}"
        };
    }

    private static int ClampYear(int? year, int fallback) =>
        Math.Clamp(year ?? fallback, 1, 9999);

    private static int ClampMonth(int? month, int fallback) =>
        Math.Clamp(month ?? fallback, 1, 12);
}
