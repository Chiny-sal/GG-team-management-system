using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain.Entities;

public class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public DateOnly WeekId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedMemberId { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.NotAssigned;
    public DateOnly? Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByMemberId { get; set; }
    public Guid? MeetingId { get; set; }

    public Group Group { get; set; } = null!;
    public Member? AssignedMember { get; set; }
    public Member CreatedByMember { get; set; } = null!;
    public Meeting? Meeting { get; set; }
}
