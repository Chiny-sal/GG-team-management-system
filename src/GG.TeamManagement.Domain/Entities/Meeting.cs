namespace GG.TeamManagement.Domain.Entities;

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly ScheduledDate { get; set; }
    public string? TopicText { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TopicSuggestion> PromotedSuggestions { get; set; } = new List<TopicSuggestion>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
