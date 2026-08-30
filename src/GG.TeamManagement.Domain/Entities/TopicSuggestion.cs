namespace GG.TeamManagement.Domain.Entities;

public class TopicSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SubmittedByTelegramUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public Guid? PromotedToMeetingId { get; set; }

    public Meeting? PromotedToMeeting { get; set; }
}
