namespace GG.TeamManagement.Application.Abstractions;

public interface ITelegramNotifier
{
    Task SendDirectMessageAsync(
        string? telegramUserId,
        string text,
        string purpose,
        string? recipientName,
        string? telegramUsername = null,
        CancellationToken cancellationToken = default);
}
