using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Common;

public static class TimePeriodParser
{
    public static TimePeriod Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "month" => TimePeriod.Month,
            "year" => TimePeriod.Year,
            _ => TimePeriod.Week
        };

    public static string ToQuery(TimePeriod period) =>
        period switch
        {
            TimePeriod.Month => "month",
            TimePeriod.Year => "year",
            _ => "week"
        };
}
