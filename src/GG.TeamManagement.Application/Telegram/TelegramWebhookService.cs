using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Telegram;

public class TelegramWebhookService
{
    private readonly IApplicationDbContext _db;

    public TelegramWebhookService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task HandleTextMessageAsync(
        long telegramUserId,
        string? telegramUsername,
        string telegramName,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var telegramId = telegramUserId.ToString();
        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.TelegramUserId == telegramId, cancellationToken);

        if (member is null)
        {
            var normalized = TelegramHandle.Normalize(telegramUsername);
            if (normalized is not null)
            {
                member = await _db.Members
                    .FirstOrDefaultAsync(m => m.TelegramUsername == normalized, cancellationToken);
                if (member is not null)
                    member.TelegramUserId = telegramId;
            }
        }
        else
        {
            var normalized = TelegramHandle.Normalize(telegramUsername);
            if (normalized is not null && member.TelegramUsername != normalized)
                member.TelegramUsername = normalized;
        }

        if (member is null) return;

        _db.TopicSuggestions.Add(new TopicSuggestion
        {
            SubmittedByTelegramUserId = telegramId,
            SubmittedByName = string.IsNullOrWhiteSpace(member.Name) ? telegramName : member.Name,
            Text = text.Trim(),
            SubmittedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
