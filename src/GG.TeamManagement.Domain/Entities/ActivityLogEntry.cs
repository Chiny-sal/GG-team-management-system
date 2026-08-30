using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain.Entities;

public class ActivityLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public ChangeType ChangeType { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Guid? ChangedByMemberId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Member? ChangedByMember { get; set; }
}
