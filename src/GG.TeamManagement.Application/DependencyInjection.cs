using GG.TeamManagement.Application.Activity;
using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Application.Dashboard;
using GG.TeamManagement.Application.Jobs;
using GG.TeamManagement.Application.Notifications;
using GG.TeamManagement.Application.Telegram;
using Microsoft.Extensions.DependencyInjection;

namespace GG.TeamManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<BoardService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<TelegramWebhookService>();
        services.AddScoped<WorkItemTelegramService>();
        services.AddScoped<MemberAssignmentCheckJob>();
        services.AddScoped<OverdueWorkCheckJob>();
        services.AddScoped<CurrentWeekRefreshJob>();
        return services;
    }
}
