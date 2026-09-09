using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Telegram;

public class WorkItemTelegramService
{
    private readonly IApplicationDbContext _db;
    private readonly ITelegramNotifier _telegram;

    public WorkItemTelegramService(IApplicationDbContext db, ITelegramNotifier telegram)
    {
        _db = db;
        _telegram = telegram;
    }

    public async Task NotifyAssignmentAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        var item = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Include(w => w.Group)
            .FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken);

        if (item?.AssignedMember is null) return;

        await _telegram.SendDirectMessageAsync(
            item.AssignedMember.TelegramUserId,
            FormatAssignment(item.Title, item.Description, item.Deadline, item.Group.Name),
            "job assignment",
            item.AssignedMember.Name,
            item.AssignedMember.TelegramUsername,
            cancellationToken);
    }

    public async Task NotifyUnassignedToLeadsAsync(
        Guid groupId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var groupName = await _db.Groups.AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(groupName)) return;

        var leads = await _db.Members.AsNoTracking()
            .Where(m => m.Role == MemberRole.Lead)
            .ToListAsync(cancellationToken);

        var text =
            $"Unassigned work was saved in {groupName}.\n\n" +
            $"Title: {title}\n" +
            "This item has no assigned member.";

        foreach (var lead in leads)
        {
            await _telegram.SendDirectMessageAsync(
                lead.TelegramUserId,
                text,
                "unassigned work alert",
                lead.Name,
                lead.TelegramUsername,
                cancellationToken);
        }
    }

    private static string FormatAssignment(string title, string description, DateOnly? deadline, string groupName)
    {
        var lines = new List<string>
        {
            $"You've been assigned work in {groupName}.",
            "",
            $"Title: {title}"
        };

        if (!string.IsNullOrWhiteSpace(description))
            lines.Add($"Description: {description.Trim()}");

        lines.Add(deadline is { } date
            ? $"Deadline: {date:yyyy-MM-dd}"
            : "Deadline: No deadline set");

        return string.Join('\n', lines);
    }

    public async Task NotifyAccountCreatedAsync(
        Guid memberId,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var member = await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null) return;

        if (string.IsNullOrWhiteSpace(member.TelegramUsername) && string.IsNullOrWhiteSpace(member.TelegramUserId))
            return;

        var text =
            $"You've been added to {member.Group.Name} on Community Board — here are your login details.\n\n" +
            $"Email: {email}\n" +
            $"Password: {password}\n\n" +
            "Use these to sign in to Community Board. You can change your password from your profile after you log in.";

        await _telegram.SendDirectMessageAsync(
            member.TelegramUserId,
            text,
            "new account",
            member.Name,
            member.TelegramUsername,
            cancellationToken);
    }
}
