namespace GG.TeamManagement.Application.Common;

public static class AssignmentWindow
{
    public const int Days = 14;

    public static DateTime SinceUtc(DateTime utcNow) => utcNow.AddDays(-Days);
}
