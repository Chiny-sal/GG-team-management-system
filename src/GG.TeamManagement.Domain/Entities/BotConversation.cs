namespace GG.TeamManagement.Domain.Entities;

public class BotConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TelegramUserId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
