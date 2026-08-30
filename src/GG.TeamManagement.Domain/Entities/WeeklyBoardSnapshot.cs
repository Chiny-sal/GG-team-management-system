namespace GG.TeamManagement.Domain.Entities;

public class WeeklyBoardSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public DateOnly WeekId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public Guid SavedByMemberId { get; set; }

    public Group Group { get; set; } = null!;
    public Member SavedByMember { get; set; } = null!;
}
