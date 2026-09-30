namespace GG.TeamManagement.Domain.Entities;

public class AttendanceAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttendanceRecordId { get; set; }
    public Guid CustomQuestionId { get; set; }
    public bool Value { get; set; }

    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public CustomQuestion CustomQuestion { get; set; } = null!;
}
