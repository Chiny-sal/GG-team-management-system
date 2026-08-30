using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? WorkItemId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    public Member? Member { get; set; }
    public WorkItem? WorkItem { get; set; }
}
