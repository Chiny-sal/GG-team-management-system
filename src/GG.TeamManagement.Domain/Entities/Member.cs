using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain.Entities;

public class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public Guid GroupId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Member;

    public Group Group { get; set; } = null!;
    public ICollection<WorkItem> AssignedWorkItems { get; set; } = new List<WorkItem>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
