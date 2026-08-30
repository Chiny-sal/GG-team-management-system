using GG.TeamManagement.Domain.Entities;

namespace GG.TeamManagement.Application.Boards;

public static class WorkItemMapper
{
    public static WorkItemDto ToDto(WorkItem item) =>
        new(
            item.Id,
            item.GroupId,
            item.WeekId,
            item.Title,
            item.Description,
            item.AssignedMemberId,
            item.AssignedMember?.Name,
            item.Status,
            item.Deadline,
            item.CreatedAt,
            item.CreatedByMemberId);
}
