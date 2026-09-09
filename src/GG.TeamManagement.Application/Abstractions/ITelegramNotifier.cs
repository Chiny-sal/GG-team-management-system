namespace GG.TeamManagement.Application.Abstractions;

public interface ITelegramNotifier
{
    Task SendDirectMessageAsync(
        string? telegramUserId,
        string text,
        string purpose,
        string? recipientName,
        CancellationToken cancellationToken = default);
}
