using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Domain.Entities;

public class DeletionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DeletionTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public Guid? RequestedByMemberId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DeletionRequestStatus Status { get; set; } = DeletionRequestStatus.Pending;
    public Guid? ResolvedByMemberId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? GroupId { get; set; }

    public Member? RequestedByMember { get; set; }
    public Member? ResolvedByMember { get; set; }
    public Group? Group { get; set; }
}
