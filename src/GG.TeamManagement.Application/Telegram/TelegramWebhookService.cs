using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Application.Telegram;

public class TelegramWebhookService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(IApplicationDbContext db, ILogger<TelegramWebhookService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleTextMessageAsync(
        long telegramUserId,
        string? telegramUsername,
        string telegramName,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("Telegram message {TelegramUserId} ignored: empty text.", telegramUserId);
            return;
        }

        var telegramId = telegramUserId.ToString();
        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.TelegramUserId == telegramId, cancellationToken);

        if (member is null)
        {
            var normalized = TelegramHandle.Normalize(telegramUsername);
            if (normalized is not null)
            {
                member = await _db.Members
                    .FirstOrDefaultAsync(
                        m => m.TelegramUsername != null && m.TelegramUsername.ToLower() == normalized,
                        cancellationToken);
                if (member is not null)
                {
                    member.TelegramUserId = telegramId;
                    _logger.LogInformation(
                        "Backfilled TelegramUserId {TelegramUserId} for member {MemberId} ({MemberName}) from username @{Username}.",
                        telegramId,
                        member.Id,
                        member.Name,
                        normalized);
                }
            }
        }
        else
        {
            var normalized = TelegramHandle.Normalize(telegramUsername);
            if (normalized is not null && member.TelegramUsername != normalized)
                member.TelegramUsername = normalized;
        }

        if (text.TrimStart().StartsWith('/'))
        {
            _logger.LogInformation(
                "Telegram command from {TelegramUserId} (@{Username}) ignored as a topic. Member matched={Matched}.",
                telegramId,
                telegramUsername,
                member is not null);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        _db.TopicSuggestions.Add(new TopicSuggestion
        {
            SubmittedByTelegramUserId = telegramId,
            SubmittedByName = string.IsNullOrWhiteSpace(member?.Name) ? telegramName : member.Name,
            Text = text.Trim(),
            SubmittedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Saved Telegram topic suggestion from {TelegramUserId} (@{Username}) member={MemberId} textLength={Length}.",
            telegramId,
            telegramUsername,
            member?.Id,
            text.Trim().Length);
    }
}
