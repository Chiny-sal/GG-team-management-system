namespace GG.TeamManagement.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsOfficeManagementTeam { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<WeeklyBoardSnapshot> Snapshots { get; set; } = new List<WeeklyBoardSnapshot>();
}
