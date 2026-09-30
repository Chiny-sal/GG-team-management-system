namespace GG.TeamManagement.Application.Abstractions;

public record TelegramButton(string Text, string CallbackData);

public interface ITelegramNotifier
{
    Task SendDirectMessageAsync(
        string? telegramUserId,
        string text,
        string purpose,
        string? recipientName,
        string? telegramUsername = null,
        CancellationToken cancellationToken = default);

    Task SendChatMessageAsync(
        long chatId,
        string text,
        string purpose,
        IReadOnlyList<TelegramButton>? buttons = null,
        CancellationToken cancellationToken = default);

    Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default);
}
