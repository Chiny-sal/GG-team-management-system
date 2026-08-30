namespace GG.TeamManagement.Domain.Entities;

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly ScheduledDate { get; set; }
    public string? TopicText { get; set; }

    public ICollection<TopicSuggestion> PromotedSuggestions { get; set; } = new List<TopicSuggestion>();
}
