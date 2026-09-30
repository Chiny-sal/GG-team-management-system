namespace GG.TeamManagement.Domain.Entities;

public class CustomQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingAttendanceId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public MeetingAttendance MeetingAttendance { get; set; } = null!;
    public ICollection<AttendanceAnswer> Answers { get; set; } = new List<AttendanceAnswer>();
}
