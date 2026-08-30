using GG.TeamManagement.Application.Jobs;
using Hangfire;

namespace GG.TeamManagement.Infrastructure.Jobs;

public static class HangfireJobRegistrar
{
    public static void RegisterRecurringJobs()
    {
        RecurringJob.AddOrUpdate<MemberAssignmentCheckJob>(
            "member-no-assignment-two-weeks",
            job => job.Execute(),
            Cron.Daily);

        RecurringJob.AddOrUpdate<OverdueWorkCheckJob>(
            "work-not-done-two-weeks",
            job => job.Execute(),
            Cron.Daily);

        RecurringJob.AddOrUpdate<CurrentWeekRefreshJob>(
            "current-week-refresh",
            job => job.Execute(),
            Cron.Weekly(DayOfWeek.Monday, 0));
    }
}
