using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GG.TeamManagement.Infrastructure.Persistence.Interceptors;

public class ActivityLoggingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IActivityFeedNotifier _notifier;
    private List<ActivityLogEntry> _pending = [];

    public ActivityLoggingInterceptor(ICurrentUser currentUser, IActivityFeedNotifier notifier)
    {
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Broadcast(CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await Broadcast(cancellationToken);
        return result;
    }

    private void Collect(DbContext? context)
    {
        _pending = [];
        if (context is null) return;

        var tracked = context.ChangeTracker.Entries()
            .Where(e => e.Entity is WorkItem or Notification or Meeting or Member or TopicSuggestion)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        EnsureMeetingsLoaded(context, tracked);
        EnsureGroupsLoaded(context, tracked);

        var actor = ActorName(context);

        foreach (var entry in tracked)
        {
            var changeType = entry.State switch
            {
                EntityState.Added => ChangeType.Created,
                EntityState.Deleted => ChangeType.Deleted,
                _ => ChangeType.Updated
            };

            var (entityType, entityId, summary) = Describe(context, entry, changeType, actor);
            if (string.IsNullOrWhiteSpace(summary))
                continue;

            var log = new ActivityLogEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                ChangeType = changeType,
                Summary = Trim(summary),
                ChangedByMemberId = _currentUser.MemberId,
                GroupId = ResolveGroupId(context, entry.Entity),
                OccurredAt = DateTime.UtcNow
            };

            _pending.Add(log);
            context.Set<ActivityLogEntry>().Add(log);
        }
    }

    private async Task Broadcast(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0) return;

        var entries = _pending.ToList();
        _pending = [];

        await _notifier.BroadcastAsync(entries, cancellationToken);
        foreach (var entityType in entries.Select(e => e.EntityType).Distinct())
            await _notifier.NotifyEntitiesChangedAsync(entityType, cancellationToken);
    }

    private string ActorName(DbContext context)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.Name))
            return _currentUser.Name;

        if (_currentUser.MemberId is Guid id)
        {
            var local = context.Set<Member>().Local.FirstOrDefault(m => m.Id == id);
            if (local is not null) return local.Name;
        }

        return "System";
    }

    private static (string EntityType, Guid EntityId, string Summary) Describe(
        DbContext context,
        EntityEntry entry,
        ChangeType changeType,
        string actor)
    {
        return entry.Entity switch
        {
            WorkItem work => ("WorkItem", work.Id, DescribeWorkItem(context, entry, work, changeType, actor)),
            Member member => ("Member", member.Id, DescribeMember(context, entry, member, changeType, actor)),
            TopicSuggestion topic => ("TopicSuggestion", topic.Id, changeType == ChangeType.Created
                ? $"{actor} added topic suggestion '{topic.Text}'."
                : string.Empty),
            Notification note => ("Notification", note.Id, DescribeNotification(note, changeType)),
            Meeting meeting => ("Meeting", meeting.Id, DescribeMeeting(entry, meeting, changeType, actor)),
            _ => (entry.Entity.GetType().Name, Guid.Empty, $"{actor} updated {entry.Entity.GetType().Name}.")
        };
    }

    private static string DescribeWorkItem(
        DbContext context,
        EntityEntry entry,
        WorkItem work,
        ChangeType changeType,
        string actor)
    {
        if (changeType == ChangeType.Created)
        {
            var linked = work.MeetingId is Guid
                ? $" and linked it to the {MeetingDateLabel(context, work.MeetingId)} meeting"
                : "";
            if (work.AssignedMemberId is Guid assignee)
                return $"{actor} added '{work.Title}' and assigned it to {MemberName(context, assignee)} as {FormatStatus(work.Status)}{linked}.";
            return $"{actor} added '{work.Title}'{linked}.";
        }

        if (changeType == ChangeType.Deleted)
            return $"{actor} deleted '{work.Title}'.";

        var parts = new List<string>();
        var title = OriginalString(entry, nameof(WorkItem.Title)) ?? work.Title;

        if (IsModified(entry, nameof(WorkItem.AssignedMemberId)))
        {
            var fromId = OriginalGuid(entry, nameof(WorkItem.AssignedMemberId));
            var toId = work.AssignedMemberId;
            var fromName = MemberName(context, fromId);
            var toName = MemberName(context, toId);

            if (fromId is null && toId is not null)
                parts.Add($"assigned '{title}' to {toName}");
            else if (fromId is not null && toId is null)
                parts.Add($"unassigned '{title}' from {fromName}");
            else
                parts.Add($"reassigned '{title}' from {fromName} to {toName}");
        }

        if (IsModified(entry, nameof(WorkItem.Status)))
        {
            var status = FormatStatus(work.Status);
            parts.Add(parts.Count == 0
                ? $"marked '{title}' as {status}"
                : $"marked it as {status}");
        }

        if (IsModified(entry, nameof(WorkItem.Title)))
        {
            var oldTitle = OriginalString(entry, nameof(WorkItem.Title)) ?? title;
            parts.Add($"renamed work item '{oldTitle}' to '{work.Title}'");
        }

        if (IsModified(entry, nameof(WorkItem.Deadline)))
        {
            parts.Add(work.Deadline is null
                ? $"cleared the due date on '{title}'"
                : $"set the due date on '{title}' to {work.Deadline:yyyy-MM-dd}");
        }

        if (IsModified(entry, nameof(WorkItem.MeetingId)))
        {
            if (work.MeetingId is Guid)
                parts.Add(parts.Count == 0
                    ? $"linked '{title}' to the {MeetingDateLabel(context, work.MeetingId)} meeting"
                    : $"linked it to the {MeetingDateLabel(context, work.MeetingId)} meeting");
            else
            {
                var previousId = OriginalGuid(entry, nameof(WorkItem.MeetingId));
                parts.Add(parts.Count == 0
                    ? $"unlinked '{title}' from the {MeetingDateLabel(context, previousId)} meeting"
                    : $"unlinked it from the {MeetingDateLabel(context, previousId)} meeting");
            }
        }

        if (IsModified(entry, nameof(WorkItem.Description)))
            parts.Add($"updated the description for '{title}'");

        if (parts.Count == 0)
            return string.Empty;

        return $"{actor} {string.Join(" and ", parts)}.";
    }

    private static string DescribeMember(
        DbContext context,
        EntityEntry entry,
        Member member,
        ChangeType changeType,
        string actor)
    {
        var groupName = GroupName(context, member.GroupId);
        if (changeType == ChangeType.Created)
            return $"{actor} added new member '{member.Name}' to {groupName}.";

        if (changeType == ChangeType.Deleted)
            return $"{actor} removed member '{member.Name}' from {groupName}.";

        var parts = new List<string>();

        if (IsModified(entry, nameof(Member.Name)))
        {
            var oldName = OriginalString(entry, nameof(Member.Name)) ?? member.Name;
            parts.Add($"renamed member '{oldName}' to '{member.Name}'");
        }

        if (IsModified(entry, nameof(Member.Role)))
        {
            if (member.Role == MemberRole.Lead)
                parts.Add($"set '{member.Name}' as Team Lead in {groupName}");
            else
                parts.Add($"removed {member.Name} as Team Lead");
        }

        if (IsModified(entry, nameof(Member.CanViewOtherGroupBoards)))
        {
            parts.Add(member.CanViewOtherGroupBoards
                ? $"granted {member.Name} view access to other groups' boards"
                : $"revoked {member.Name}'s view access to other groups' boards");
        }

        if (IsModified(entry, nameof(Member.CanAssignWorkToOtherGroups)))
        {
            parts.Add(member.CanAssignWorkToOtherGroups
                ? $"granted {member.Name} permission to assign work to other groups"
                : $"revoked {member.Name}'s permission to assign work to other groups");
        }

        if (IsModified(entry, nameof(Member.TelegramUsername)))
            parts.Add($"updated the Telegram username for '{member.Name}'");

        if (parts.Count == 0)
            return string.Empty;

        return $"{actor} {string.Join(" and ", parts)}.";
    }

    private static string DescribeNotification(Notification note, ChangeType changeType)
    {
        if (changeType != ChangeType.Created)
            return string.Empty;

        return note.Type switch
        {
            NotificationType.MemberNoAssignmentTwoWeeks =>
                "System flagged a member with no assignment for two weeks.",
            NotificationType.WorkNotDoneTwoWeeks =>
                "System flagged a work item that has been incomplete for two weeks.",
            _ => $"System created a '{note.Type}' notification."
        };
    }

    private static string DescribeMeeting(
        EntityEntry entry,
        Meeting meeting,
        ChangeType changeType,
        string actor)
    {
        var dateLabel = MeetingDateFormatter.ToOrdinalDate(meeting.ScheduledDate);

        if (changeType == ChangeType.Created)
        {
            return string.IsNullOrWhiteSpace(meeting.TopicText)
                ? $"{actor} created the {dateLabel} meeting."
                : $"{actor} set the meeting topic to '{meeting.TopicText}'.";
        }

        if (IsModified(entry, nameof(Meeting.Notes)))
        {
            var original = OriginalString(entry, nameof(Meeting.Notes));
            return string.IsNullOrWhiteSpace(original)
                ? $"{actor} added notes to the {dateLabel} meeting."
                : $"{actor} edited notes on the {dateLabel} meeting.";
        }

        if (IsModified(entry, nameof(Meeting.TopicText)))
        {
            return string.IsNullOrWhiteSpace(meeting.TopicText)
                ? $"{actor} updated the {dateLabel} meeting."
                : $"{actor} set the meeting topic to '{meeting.TopicText}'.";
        }

        return string.Empty;
    }

    private static void EnsureMeetingsLoaded(DbContext context, List<EntityEntry> tracked)
    {
        var ids = new HashSet<Guid>();
        foreach (var entry in tracked)
        {
            if (entry.Entity is not WorkItem work)
                continue;
            if (work.MeetingId is Guid current)
                ids.Add(current);
            if (entry.State == EntityState.Modified)
            {
                var previous = OriginalGuid(entry, nameof(WorkItem.MeetingId));
                if (previous is Guid previousId)
                    ids.Add(previousId);
            }
        }

        ids.RemoveWhere(id => context.Set<Meeting>().Local.Any(m => m.Id == id));
        if (ids.Count == 0)
            return;

        context.Set<Meeting>().Where(m => ids.Contains(m.Id)).Load();
    }

    private static void EnsureGroupsLoaded(DbContext context, List<EntityEntry> tracked)
    {
        var ids = tracked
            .Select(e => e.Entity switch
            {
                Member member => member.GroupId,
                WorkItem work => work.GroupId,
                _ => Guid.Empty
            })
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        ids.RemoveWhere(id => context.Set<Group>().Local.Any(g => g.Id == id));
        if (ids.Count == 0)
            return;

        context.Set<Group>().Where(g => ids.Contains(g.Id)).Load();
    }

    private static Guid? ResolveGroupId(DbContext context, object entity)
    {
        switch (entity)
        {
            case WorkItem work:
                return work.GroupId;
            case Member member:
                return member.GroupId;
            case Notification note:
                if (note.MemberId is Guid memberId)
                {
                    var local = context.Set<Member>().Local.FirstOrDefault(m => m.Id == memberId);
                    if (local is not null) return local.GroupId;
                    return context.Set<Member>().AsNoTracking()
                        .Where(m => m.Id == memberId)
                        .Select(m => (Guid?)m.GroupId)
                        .FirstOrDefault();
                }

                if (note.WorkItemId is Guid workItemId)
                {
                    var local = context.Set<WorkItem>().Local.FirstOrDefault(w => w.Id == workItemId);
                    if (local is not null) return local.GroupId;
                    return context.Set<WorkItem>().AsNoTracking()
                        .Where(w => w.Id == workItemId)
                        .Select(w => (Guid?)w.GroupId)
                        .FirstOrDefault();
                }

                return null;
            default:
                return null;
        }
    }

    private static string MeetingDateLabel(DbContext context, Guid? meetingId)
    {
        if (meetingId is null)
            return "unknown";

        var meeting = context.Set<Meeting>().Local.FirstOrDefault(m => m.Id == meetingId.Value);
        return meeting is null
            ? "unknown"
            : MeetingDateFormatter.ToOrdinalDate(meeting.ScheduledDate);
    }

    private static bool IsModified(EntityEntry entry, string propertyName) =>
        entry.State == EntityState.Modified && entry.Property(propertyName).IsModified;

    private static string? OriginalString(EntityEntry entry, string propertyName) =>
        entry.Property(propertyName).OriginalValue as string;

    private static Guid? OriginalGuid(EntityEntry entry, string propertyName)
    {
        var value = entry.Property(propertyName).OriginalValue;
        return value is Guid guid ? guid : null;
    }

    private static string MemberName(DbContext context, Guid? memberId)
    {
        if (memberId is null) return "Unassigned";
        return context.Set<Member>().Local.FirstOrDefault(m => m.Id == memberId)?.Name
            ?? "Unknown member";
    }

    private static string GroupName(DbContext context, Guid groupId)
    {
        return context.Set<Group>().Local.FirstOrDefault(g => g.Id == groupId)?.Name
            ?? "the group";
    }

    private static string FormatStatus(WorkItemStatus status) => status switch
    {
        WorkItemStatus.NotAssigned => "Not Assigned",
        WorkItemStatus.NotDone => "Not Done",
        _ => status.ToString()
    };

    private static string Trim(string summary) =>
        summary.Length <= 500 ? summary : summary[..497] + "...";
}
