using GG.TeamManagement.Application.Jobs;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Infrastructure.Jobs;

public static class HangfireJobRegistrar
{
    public static void RegisterRecurringJobs(ILogger logger)
    {
        logger.LogInformation("Hangfire: registering recurring jobs");
        Console.WriteLine("[Startup] Hangfire: registering recurring jobs");
        Console.Out.Flush();

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

        logger.LogInformation("Hangfire: recurring jobs registered");
        Console.WriteLine("[Startup] Hangfire: recurring jobs registered");
        Console.Out.Flush();
    }
}
