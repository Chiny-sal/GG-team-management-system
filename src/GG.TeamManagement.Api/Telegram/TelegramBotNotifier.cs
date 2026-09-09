using GG.TeamManagement.Infrastructure.Persistence;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using Telegram.Bot;
using Telegram.Bot.Types;

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
        string? telegramUsername = null,
        CancellationToken cancellationToken = default)
    {
        var who = string.IsNullOrWhiteSpace(recipientName) ? "member" : recipientName;

        ChatId? chat = null;
        if (long.TryParse(telegramUserId, out var chatId))
            chat = chatId;
        else
        {
            var username = TelegramHandle.Normalize(telegramUsername);
            if (username is not null)
                chat = new ChatId($"@{username}");
        }

        if (chat is null)
        {
            _logger.LogInformation(
                "Skipping Telegram DM ({Purpose}) for {Recipient}: no TelegramUserId or TelegramUsername.",
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

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var bot = new TelegramBotClient(_token);
            await bot.SendMessage(chat, text, cancellationToken: timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send Telegram DM ({Purpose}) to {Recipient} ({Chat}).",
                purpose,
                who,
                chat);
        }
    }
}
