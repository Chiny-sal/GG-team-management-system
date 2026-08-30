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

        if (token is null || publicApiUrl is null)
            return "not configured";

        var webhookUrl = $"{publicApiUrl.TrimEnd('/')}/api/telegram/webhook";
        var secret = AppEnvironment.Optional(configuration, AppEnvironment.TelegramWebhookSecret);

        try
        {
            var bot = new TelegramBotClient(token);
            await bot.SetWebhook(
                url: webhookUrl,
                secretToken: string.IsNullOrWhiteSpace(secret) ? null : secret,
                cancellationToken: cancellationToken);
            return "registered";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Telegram webhook at {WebhookUrl}", webhookUrl);
            return "registration failed";
        }
    }
}
