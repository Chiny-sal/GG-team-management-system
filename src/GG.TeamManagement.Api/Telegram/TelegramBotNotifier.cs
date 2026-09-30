using GG.TeamManagement.Infrastructure.Persistence;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

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

        if (!long.TryParse(telegramUserId, out var chatId))
        {
            var username = TelegramHandle.Normalize(telegramUsername);
            _logger.LogWarning(
                "Skipping Telegram DM ({Purpose}) for {Recipient}: no numeric TelegramUserId (username={Username}). Private DMs require the numeric id, which is stored only after that person messages the bot.",
                purpose,
                who,
                username ?? "(none)");
            return;
        }

        await SendChatMessageAsync(chatId, text, purpose, buttons: null, cancellationToken);
    }

    public async Task SendChatMessageAsync(
        long chatId,
        string text,
        string purpose,
        IReadOnlyList<TelegramButton>? buttons = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogInformation(
                "Skipping Telegram message ({Purpose}) to {Chat}: bot token is not configured.",
                purpose,
                chatId);
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var bot = new TelegramBotClient(_token);
            ReplyMarkup? markup = null;
            if (buttons is { Count: > 0 })
            {
                var rows = new List<InlineKeyboardButton[]>();
                for (var i = 0; i < buttons.Count; i += 2)
                {
                    rows.Add(buttons.Skip(i).Take(2)
                        .Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackData))
                        .ToArray());
                }
                markup = new InlineKeyboardMarkup(rows);
            }

            await bot.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: timeout.Token);
            _logger.LogInformation("Sent Telegram message ({Purpose}) to {Chat}.", purpose, chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message ({Purpose}) to {Chat}.", purpose, chatId);
        }
    }

    public async Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(callbackQueryId))
            return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var bot = new TelegramBotClient(_token);
            await bot.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to answer Telegram callback {CallbackQueryId}.", callbackQueryId);
        }
    }
}
