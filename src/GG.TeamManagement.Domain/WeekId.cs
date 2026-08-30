namespace GG.TeamManagement.Domain;

/// <summary>
/// Monday-anchored week identifier used across boards, jobs, and the UI.
/// </summary>
public static class WeekId
{
    public static DateOnly FromDate(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    public static DateOnly FromDateTime(DateTime dateTime) =>
        FromDate(DateOnly.FromDateTime(dateTime));

    public static DateOnly CurrentUtc() => FromDateTime(DateTime.UtcNow);
}
