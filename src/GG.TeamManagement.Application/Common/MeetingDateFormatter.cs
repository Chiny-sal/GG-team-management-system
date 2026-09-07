using System.Globalization;

namespace GG.TeamManagement.Application.Common;

public static class MeetingDateFormatter
{
    public static string ToOrdinalDate(DateOnly date)
    {
        var day = date.Day;
        var suffix = (day % 10, day) switch
        {
            (1, not 11) => "st",
            (2, not 12) => "nd",
            (3, not 13) => "rd",
            _ => "th"
        };

        return $"{date.ToString("MMMM", CultureInfo.InvariantCulture)} {day}{suffix}";
    }
}
