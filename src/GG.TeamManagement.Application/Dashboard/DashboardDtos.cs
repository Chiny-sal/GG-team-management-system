using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Dashboard;

public record MeetingDto(Guid Id, DateOnly ScheduledDate, string? TopicText);

public record TopicSuggestionDto(
    Guid Id,
    string SubmittedByTelegramUserId,
    string SubmittedByName,
    string Text,
    DateTime SubmittedAt,
    Guid? PromotedToMeetingId);

public record GroupSummaryDto(
    Guid GroupId,
    string GroupName,
    int AssignedCount,
    int DoneCount,
    int NotDoneCount,
    int OngoingCount,
    int UnassignedCount);

public record DashboardDto(
    MeetingDto? CurrentMeeting,
    IReadOnlyList<TopicSuggestionDto> PendingSuggestions,
    IReadOnlyList<GroupSummaryDto> GroupSummaries,
    IReadOnlyList<Boards.WorkItemDto> DoneThisWeek,
    IReadOnlyList<Boards.WorkItemDto> NotDoneThisWeek,
    IReadOnlyList<Boards.WorkItemDto> AssignedThisWeek);
