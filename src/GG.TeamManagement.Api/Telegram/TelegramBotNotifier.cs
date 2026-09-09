using GG.TeamManagement.Infrastructure.Persistence;
using GG.TeamManagement.Application.Abstractions;
using Telegram.Bot;

namespace GG.TeamManagement.Api.Telegram;

public sealed class TelegramBotNotifier : ITelegramNotifier
{
    private readonly string? _token;
    private readonly ILogger<TelegramBotNotifier> _logger;

    public TelegramBotNotifier(IConfiguration configuration, ILogger<TelegramBotNotifier> logger)
    {
        _token = AppEnvironment.Optional(configuration, AppEnvironment.TelegramBotToken);
        _logger = logger;
    }

    public async Task SendDirectMessageAsync(
        string? telegramUserId,
        string text,
        string purpose,
        string? recipientName,
        CancellationToken cancellationToken = default)
    {
        var who = string.IsNullOrWhiteSpace(recipientName) ? "member" : recipientName;

        if (string.IsNullOrWhiteSpace(telegramUserId))
        {
            _logger.LogInformation(
                "Skipping Telegram DM ({Purpose}) for {Recipient}: no TelegramUserId.",
                purpose,
                who);
            return;
        }

        if (string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogInformation(
                "Skipping Telegram DM ({Purpose}) for {Recipient}: bot token is not configured.",
                purpose,
                who);
            return;
        }

        if (!long.TryParse(telegramUserId, out var chatId))
        {
            _logger.LogInformation(
                "Skipping Telegram DM ({Purpose}) for {Recipient}: TelegramUserId {TelegramUserId} is not a numeric chat id.",
                purpose,
                who,
                telegramUserId);
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var bot = new TelegramBotClient(_token);
            await bot.SendMessage(chatId, text, cancellationToken: timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send Telegram DM ({Purpose}) to {Recipient} ({ChatId}).",
                purpose,
                who,
                chatId);
        }
    }
}
