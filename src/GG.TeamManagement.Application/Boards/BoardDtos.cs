using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Boards;

public record GroupDto(Guid Id, string Name, bool IsOfficeManagementTeam);

public record MemberDto(Guid Id, string Name, Guid GroupId, MemberRole Role, string? TelegramUserId, string? TelegramUsername);

public record WorkItemDto(
    Guid Id,
    Guid GroupId,
    DateOnly WeekId,
    string Title,
    string Description,
    Guid? AssignedMemberId,
    string? AssignedMemberName,
    WorkItemStatus Status,
    DateOnly? Deadline,
    DateTime CreatedAt,
    Guid CreatedByMemberId,
    Guid? MeetingId);

public record WorkItemDetailDto(
    Guid Id,
    Guid GroupId,
    string GroupName,
    DateOnly WeekId,
    string Title,
    string Description,
    Guid? AssignedMemberId,
    string? AssignedMemberName,
    WorkItemStatus Status,
    DateOnly? Deadline,
    DateTime CreatedAt,
    Guid CreatedByMemberId,
    string? CreatedByMemberName,
    Guid? MeetingId,
    string? MeetingTopicText,
    DateOnly? MeetingScheduledDate);

public record WorkRegistryDto(
    Guid GroupId,
    string GroupName,
    IReadOnlyList<WorkItemDto> WorkItems);

public record MeetingSummaryDto(Guid Id, DateOnly ScheduledDate, string? TopicText);

public record CreateWorkItemRequest(string Title, string? Description, DateOnly? Deadline, Guid? MeetingId = null);

public record UpdateWorkItemRequest(
    string? Title,
    string? Description,
    Guid? AssignedMemberId,
    bool ClearAssignment,
    WorkItemStatus? Status,
    DateOnly? Deadline,
    bool ClearDeadline = false,
    Guid? MeetingId = null,
    bool ClearMeeting = false,
    Guid? GroupId = null);

public record BoardDto(
    Guid GroupId,
    string GroupName,
    DateOnly WeekId,
    string Period,
    string PeriodLabel,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    bool IsLocked,
    DateTime? SavedAt,
    IReadOnlyList<DateOnly> LockedWeekIds,
    IReadOnlyList<MemberDto> Members,
    IReadOnlyList<WorkItemDto> WorkItems,
    IReadOnlyList<MeetingSummaryDto> Meetings);

public record SaveBoardResponse(Guid SnapshotId, DateTime SavedAt, DateOnly WeekId);

public record MemberNameUpdate(Guid Id, string Name);

public record WorkItemCommit(
    Guid Id,
    bool IsNew,
    string Title,
    string? Description,
    Guid? AssignedMemberId,
    WorkItemStatus Status,
    DateOnly? Deadline,
    Guid? MeetingId,
    Guid? GroupId = null);

public record CommitBoardRequest(
    IReadOnlyList<MemberNameUpdate>? MemberUpdates,
    IReadOnlyList<WorkItemCommit>? WorkItems);

public record AddMemberRequest(string Name, string Email, string Password, string? TelegramUserId, string? TelegramUsername = null);

public record RenameGroupRequest(string Name);
