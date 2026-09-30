namespace GG.TeamManagement.Domain.Entities;

public class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingAttendanceId { get; set; }
    public Guid MemberId { get; set; }
    public bool Present { get; set; }
    public bool AttendedWeeklyClass { get; set; }

    public MeetingAttendance MeetingAttendance { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public ICollection<AttendanceAnswer> Answers { get; set; } = new List<AttendanceAnswer>();
}
