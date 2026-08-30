using GG.TeamManagement.Domain.Entities;

namespace GG.TeamManagement.Application.Abstractions;

public interface IActivityFeedNotifier
{
    Task BroadcastAsync(IReadOnlyList<ActivityLogEntry> entries, CancellationToken cancellationToken = default);
    Task NotifyEntitiesChangedAsync(string entityType, CancellationToken cancellationToken = default);
}
