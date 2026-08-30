using Telegram.Bot;

namespace GG.TeamManagement.Api.Telegram;

public class TelegramWebhookSetupHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramWebhookSetupHostedService> _logger;

    public TelegramWebhookSetupHostedService(
        IConfiguration configuration,
        ILogger<TelegramWebhookSetupHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = _configuration["Telegram:BotToken"] ?? _configuration["TELEGRAM_BOT_TOKEN"];
        var publicApiUrl = _configuration["PUBLIC_API_URL"] ?? _configuration["Telegram:PublicApiUrl"];

        if (string.IsNullOrWhiteSpace(token) || token.Contains("<<", StringComparison.Ordinal))
        {
            _logger.LogWarning("Telegram:BotToken / TELEGRAM_BOT_TOKEN is not configured; webhook was not registered.");
            return;
        }

        if (string.IsNullOrWhiteSpace(publicApiUrl) || publicApiUrl.Contains("<<", StringComparison.Ordinal))
        {
            _logger.LogWarning("PUBLIC_API_URL is not configured; webhook was not registered.");
            return;
        }

        var webhookUrl = $"{publicApiUrl.TrimEnd('/')}/api/telegram/webhook";
        var secret = _configuration["Telegram:WebhookSecret"];

        try
        {
            var bot = new TelegramBotClient(token);
            await bot.SetWebhook(
                url: webhookUrl,
                secretToken: string.IsNullOrWhiteSpace(secret) ? null : secret,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Telegram webhook registered at {WebhookUrl}", webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Telegram webhook at {WebhookUrl}", webhookUrl);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
