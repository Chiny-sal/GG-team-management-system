namespace GG.TeamManagement.Application.Attendance;

public record AttendanceRosterMemberDto(
    Guid Id,
    string Name,
    string GroupName,
    bool HasTelegram);

public record AttendanceSessionSummaryDto(
    Guid Id,
    DateOnly Date,
    DateTime CreatedAt,
    string CreatedByName,
    int PresentCount,
    int MemberCount);

public record AttendanceQuestionDto(Guid Id, string QuestionText, int SortOrder);

public record AttendanceAnswerDto(Guid QuestionId, bool Value);

public record AttendanceRecordDto(
    Guid MemberId,
    string MemberName,
    string GroupName,
    bool Present,
    bool AttendedWeeklyClass,
    IReadOnlyList<AttendanceAnswerDto> Answers);

public record AttendanceSessionDto(
    Guid Id,
    DateOnly Date,
    DateTime CreatedAt,
    string CreatedByName,
    IReadOnlyList<AttendanceQuestionDto> Questions,
    IReadOnlyList<AttendanceRecordDto> Records);

public record SaveAttendanceRequest(
    DateOnly? Date,
    IReadOnlyList<string>? Questions,
    IReadOnlyList<SaveAttendanceRowRequest> Rows);

public record SaveAttendanceRowRequest(
    Guid MemberId,
    bool Present,
    bool AttendedWeeklyClass,
    Dictionary<string, bool>? Answers);

public record UpdateAttendanceRequest(IReadOnlyList<SaveAttendanceRowRequest> Rows);

public record BroadcastMessageRequest(string Message, IReadOnlyList<Guid> MemberIds);

public record BroadcastMessageResult(int Sent, int Skipped);
