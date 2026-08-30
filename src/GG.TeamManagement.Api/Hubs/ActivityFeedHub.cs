using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GG.TeamManagement.Api.Hubs;

[Authorize]
public class ActivityFeedHub : Hub
{
    public const string GroupName = "activity-feed";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        await base.OnConnectedAsync();
    }
}
