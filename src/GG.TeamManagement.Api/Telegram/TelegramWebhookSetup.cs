using GG.TeamManagement.Infrastructure.Persistence;
using Telegram.Bot;

namespace GG.TeamManagement.Api.Telegram;

public static class TelegramWebhookSetup
{
    public static async Task<string> TryRegisterAsync(
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var token = AppEnvironment.Optional(configuration, AppEnvironment.TelegramBotToken);
        var publicApiUrl = AppEnvironment.Optional(configuration, AppEnvironment.PublicApiUrl);

        logger.LogInformation(
            "Telegram webhook config: token={TokenConfigured}; PUBLIC_API_URL={PublicApiUrl}.",
            token is null ? "missing" : "set",
            publicApiUrl ?? "(not set)");

        if (token is null || publicApiUrl is null)
            return "not configured";

        if (publicApiUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || publicApiUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "PUBLIC_API_URL is {PublicApiUrl}. Telegram cannot deliver webhooks to localhost; topic suggestions and TelegramUserId backfill will not work in production until this is the public Render HTTPS URL.",
                publicApiUrl);
        }

        var webhookUrl = $"{publicApiUrl.TrimEnd('/')}/api/telegram/webhook";
        var secret = AppEnvironment.Optional(configuration, AppEnvironment.TelegramWebhookSecret);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var bot = new TelegramBotClient(token);
            await bot.SetWebhook(
                url: webhookUrl,
                secretToken: string.IsNullOrWhiteSpace(secret) ? null : secret,
                cancellationToken: timeout.Token);
            var info = await bot.GetWebhookInfo(timeout.Token);
            logger.LogInformation(
                "Telegram webhook registered at {WebhookUrl}. Telegram reports url={ReportedUrl} pending={Pending} lastErrorDate={LastErrorDate} lastError={LastError}.",
                webhookUrl,
                info.Url,
                info.PendingUpdateCount,
                info.LastErrorDate,
                info.LastErrorMessage);
            return "registered";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Telegram webhook at {WebhookUrl}", webhookUrl);
            return "registration failed";
        }
    }
}
