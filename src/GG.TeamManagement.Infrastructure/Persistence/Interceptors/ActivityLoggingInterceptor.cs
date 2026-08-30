using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
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
            .Where(e => e.Entity is WorkItem or WeeklyBoardSnapshot or Notification or Meeting)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in tracked)
        {
            var changeType = entry.State switch
            {
                EntityState.Added => ChangeType.Created,
                EntityState.Deleted => ChangeType.Deleted,
                _ => ChangeType.Updated
            };

            var (entityType, entityId, summary) = Describe(entry.Entity, changeType);
            var log = new ActivityLogEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                ChangeType = changeType,
                Summary = summary,
                ChangedByMemberId = _currentUser.MemberId,
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

    private static (string EntityType, Guid EntityId, string Summary) Describe(object entity, ChangeType changeType)
    {
        var verb = changeType switch
        {
            ChangeType.Created => "created",
            ChangeType.Deleted => "deleted",
            _ => "updated"
        };

        return entity switch
        {
            WorkItem work => ("WorkItem", work.Id, $"Work item '{work.Title}' {verb}."),
            WeeklyBoardSnapshot snap => ("WeeklyBoardSnapshot", snap.Id, $"Weekly board snapshot for group {snap.GroupId} week {snap.WeekId:yyyy-MM-dd} {verb}."),
            Notification note => ("Notification", note.Id, $"Notification '{note.Type}' {verb}."),
            Meeting meeting => ("Meeting", meeting.Id, string.IsNullOrWhiteSpace(meeting.TopicText)
                ? $"Meeting on {meeting.ScheduledDate:yyyy-MM-dd} {verb}."
                : $"Meeting topic set to '{meeting.TopicText}'."),
            _ => (entity.GetType().Name, Guid.Empty, $"{entity.GetType().Name} {verb}.")
        };
    }
}
