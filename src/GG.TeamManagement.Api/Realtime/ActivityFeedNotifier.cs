using GG.TeamManagement.Api.Hubs;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Activity;
using GG.TeamManagement.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace GG.TeamManagement.Api.Realtime;

public class ActivityFeedNotifier : IActivityFeedNotifier
{
    private readonly IHubContext<ActivityFeedHub> _hub;

    public ActivityFeedNotifier(IHubContext<ActivityFeedHub> hub)
    {
        _hub = hub;
    }

    public async Task BroadcastAsync(IReadOnlyList<ActivityLogEntry> entries, CancellationToken cancellationToken = default)
    {
        var payload = entries.Select(e => new ActivityLogDto(
            e.Id,
            e.EntityType,
            e.EntityId,
            e.ChangeType,
            e.Summary,
            e.ChangedByMemberId,
            null,
            e.OccurredAt)).ToList();

        await _hub.Clients.Group(ActivityFeedHub.GroupName)
            .SendAsync("ActivityLogged", payload, cancellationToken);
    }

    public Task NotifyEntitiesChangedAsync(string entityType, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(ActivityFeedHub.GroupName)
            .SendAsync("EntitiesChanged", entityType, cancellationToken);
}
