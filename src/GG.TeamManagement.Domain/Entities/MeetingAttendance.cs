namespace GG.TeamManagement.Domain.Entities;

public class MeetingAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public Guid CreatedByMemberId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Member CreatedByMember { get; set; } = null!;
    public ICollection<CustomQuestion> CustomQuestions { get; set; } = new List<CustomQuestion>();
    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
