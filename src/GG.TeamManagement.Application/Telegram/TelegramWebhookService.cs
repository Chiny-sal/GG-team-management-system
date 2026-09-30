using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Application.Telegram;

public class TelegramWebhookService
{
    public static readonly TimeSpan ConversationTtl = TimeSpan.FromMinutes(5);

    private static readonly TelegramButton[] StatusButtons =
    [
        new("Assigned", "status:Assigned"),
        new("Ongoing", "status:Ongoing"),
        new("Done", "status:Done"),
        new("Not Done", "status:NotDone")
    ];

    private readonly IApplicationDbContext _db;
    private readonly ITelegramNotifier _telegram;
    private readonly IAmbientActor _ambient;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        IApplicationDbContext db,
        ITelegramNotifier telegram,
        IAmbientActor ambient,
        ILogger<TelegramWebhookService> logger)
    {
        _db = db;
        _telegram = telegram;
        _ambient = ambient;
        _logger = logger;
    }

    public async Task HandleTextMessageAsync(
        long telegramUserId,
        long chatId,
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

        var member = await ResolveMemberAsync(telegramUserId, telegramUsername, cancellationToken);
        var conversation = await GetActiveConversationAsync(telegramUserId.ToString(), cancellationToken);

        if (BotCommands.Is(text, "update"))
        {
            await StartUpdateAsync(chatId, telegramUserId, member, cancellationToken);
            return;
        }

        if (BotCommands.Is(text, "meeting"))
        {
            await StartMeetingAsync(chatId, telegramUserId, cancellationToken);
            return;
        }

        if (conversation is not null && BotCommands.IsCancel(text))
        {
            await ClearConversationAsync(conversation, cancellationToken);
            await ReplyAsync(chatId, "Cancelled.", "bot cancel", cancellationToken);
            return;
        }

        if (conversation?.CurrentStep == BotConversationSteps.AwaitingWorkCode)
        {
            await HandleWorkCodeAsync(chatId, conversation, text, cancellationToken);
            return;
        }

        if (conversation?.CurrentStep == BotConversationSteps.AwaitingStatus)
        {
            await ReplyAsync(
                chatId,
                "Tap one of the status buttons, or send cancel.",
                "bot awaiting status",
                cancellationToken,
                StatusButtons);
            return;
        }

        if (conversation?.CurrentStep == BotConversationSteps.AwaitingMeetingTopic)
        {
            await SaveTopicSuggestionAsync(telegramUserId, telegramName, member, text, cancellationToken);
            await ClearConversationAsync(conversation, cancellationToken);
            await ReplyAsync(chatId, "Added to the suggestions list.", "bot meeting saved", cancellationToken);
            return;
        }

        if (text.TrimStart().StartsWith('/'))
        {
            _logger.LogInformation(
                "Telegram command from {TelegramUserId} (@{Username}) ignored as a topic. Member matched={Matched}.",
                telegramUserId,
                telegramUsername,
                member is not null);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        await SaveTopicSuggestionAsync(telegramUserId, telegramName, member, text, cancellationToken);
        _logger.LogInformation(
            "Saved Telegram topic suggestion from {TelegramUserId} (@{Username}) member={MemberId} textLength={Length}.",
            telegramUserId,
            telegramUsername,
            member?.Id,
            text.Trim().Length);
    }

    public async Task HandleCallbackQueryAsync(
        long telegramUserId,
        long chatId,
        string callbackQueryId,
        string? data,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetActiveConversationAsync(telegramUserId.ToString(), cancellationToken);
        if (conversation is null
            || conversation.CurrentStep != BotConversationSteps.AwaitingStatus
            || string.IsNullOrWhiteSpace(data)
            || !data.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, "That choice expired. Send update to start again.", cancellationToken);
            return;
        }

        var statusName = data["status:".Length..];
        if (!Enum.TryParse<WorkItemStatus>(statusName, ignoreCase: true, out var status)
            || status is WorkItemStatus.NotAssigned)
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, "Unknown status.", cancellationToken);
            return;
        }

        if (!Guid.TryParse(conversation.Payload, out var workItemId))
        {
            await ClearConversationAsync(conversation, cancellationToken);
            await _telegram.AnswerCallbackAsync(callbackQueryId, "Start again with update.", cancellationToken);
            return;
        }

        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.TelegramUserId == telegramUserId.ToString(), cancellationToken);
        if (member is null)
        {
            await ClearConversationAsync(conversation, cancellationToken);
            await _telegram.AnswerCallbackAsync(callbackQueryId, "You need to be a registered member first.", cancellationToken);
            await ReplyAsync(chatId, "You need to be a registered member first.", "bot unregistered", cancellationToken);
            return;
        }

        var item = await _db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);
        if (item is null)
        {
            await ClearConversationAsync(conversation, cancellationToken);
            await _telegram.AnswerCallbackAsync(callbackQueryId, "That job is gone.", cancellationToken);
            await ReplyAsync(chatId, "That job no longer exists. Send update to try another code.", "bot missing job", cancellationToken);
            return;
        }

        item.Status = status;
        _ambient.Set(member.Name, member.Id, member.GroupId, viaTelegram: true);
        await ClearConversationAsync(conversation, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var label = FormatStatus(status);
        await _telegram.AnswerCallbackAsync(callbackQueryId, $"Updated to {label}.", cancellationToken);
        await ReplyAsync(chatId, $"Updated '{item.Title}' to {label}.", "bot status updated", cancellationToken);
    }

    private async Task StartUpdateAsync(
        long chatId,
        long telegramUserId,
        Member? member,
        CancellationToken cancellationToken)
    {
        if (member is null)
        {
            await ReplyAsync(
                chatId,
                "You need to be a registered member first. Ask Office Management to add you, then send this bot a message so we can link your Telegram.",
                "bot unregistered",
                cancellationToken);
            return;
        }

        await UpsertConversationAsync(telegramUserId.ToString(), BotConversationSteps.AwaitingWorkCode, null, cancellationToken);
        await ReplyAsync(
            chatId,
            "Which job? Send its code, like WORK-142. Send cancel to stop.",
            "bot ask code",
            cancellationToken);
    }

    private async Task StartMeetingAsync(long chatId, long telegramUserId, CancellationToken cancellationToken)
    {
        await UpsertConversationAsync(telegramUserId.ToString(), BotConversationSteps.AwaitingMeetingTopic, null, cancellationToken);
        await ReplyAsync(chatId, "What would you like to suggest as a topic?", "bot ask topic", cancellationToken);
    }

    private async Task HandleWorkCodeAsync(
        long chatId,
        BotConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var code = WorkItemCodes.Normalize(text);
        var item = code is null
            ? null
            : await _db.WorkItems.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Code.ToLower() == code.ToLower(), cancellationToken);

        if (item is null)
        {
            await TouchConversationAsync(conversation, cancellationToken);
            await ReplyAsync(
                chatId,
                $"I couldn't find a job with code {text.Trim()}. Check the code and try again, or send cancel.",
                "bot unknown code",
                cancellationToken);
            return;
        }

        conversation.CurrentStep = BotConversationSteps.AwaitingStatus;
        conversation.Payload = item.Id.ToString();
        await TouchConversationAsync(conversation, cancellationToken);

        await ReplyAsync(
            chatId,
            $"{item.Code} — {item.Title}\nCurrent status: {FormatStatus(item.Status)}\n\nChoose a new status:",
            "bot status options",
            cancellationToken,
            StatusButtons);
    }

    private async Task SaveTopicSuggestionAsync(
        long telegramUserId,
        string telegramName,
        Member? member,
        string text,
        CancellationToken cancellationToken)
    {
        _db.TopicSuggestions.Add(new TopicSuggestion
        {
            SubmittedByTelegramUserId = telegramUserId.ToString(),
            SubmittedByName = string.IsNullOrWhiteSpace(member?.Name) ? telegramName : member.Name,
            Text = text.Trim(),
            SubmittedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Member?> ResolveMemberAsync(
        long telegramUserId,
        string? telegramUsername,
        CancellationToken cancellationToken)
    {
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
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        else
        {
            var normalized = TelegramHandle.Normalize(telegramUsername);
            if (normalized is not null && member.TelegramUsername != normalized)
            {
                member.TelegramUsername = normalized;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return member;
    }

    private async Task<BotConversation?> GetActiveConversationAsync(string telegramUserId, CancellationToken cancellationToken)
    {
        var conversation = await _db.BotConversations
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId, cancellationToken);
        if (conversation is null) return null;

        if (conversation.ExpiresAt <= DateTime.UtcNow)
        {
            _db.BotConversations.Remove(conversation);
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        return conversation;
    }

    private async Task UpsertConversationAsync(
        string telegramUserId,
        string step,
        string? payload,
        CancellationToken cancellationToken)
    {
        var conversation = await _db.BotConversations
            .FirstOrDefaultAsync(c => c.TelegramUserId == telegramUserId, cancellationToken);
        var now = DateTime.UtcNow;
        if (conversation is null)
        {
            conversation = new BotConversation { TelegramUserId = telegramUserId };
            _db.BotConversations.Add(conversation);
        }

        conversation.CurrentStep = step;
        conversation.Payload = payload;
        conversation.UpdatedAt = now;
        conversation.ExpiresAt = now.Add(ConversationTtl);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task TouchConversationAsync(BotConversation conversation, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        conversation.UpdatedAt = now;
        conversation.ExpiresAt = now.Add(ConversationTtl);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearConversationAsync(BotConversation conversation, CancellationToken cancellationToken)
    {
        _db.BotConversations.Remove(conversation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Task ReplyAsync(
        long chatId,
        string text,
        string purpose,
        CancellationToken cancellationToken,
        IReadOnlyList<TelegramButton>? buttons = null) =>
        _telegram.SendChatMessageAsync(chatId, text, purpose, buttons, cancellationToken);

    private static string FormatStatus(WorkItemStatus status) => status switch
    {
        WorkItemStatus.NotAssigned => "Unassigned",
        WorkItemStatus.NotDone => "Not Done",
        _ => status.ToString()
    };
}
