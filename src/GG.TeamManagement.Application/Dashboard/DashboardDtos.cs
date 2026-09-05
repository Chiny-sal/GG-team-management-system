using GG.TeamManagement.Application.Boards;

namespace GG.TeamManagement.Application.Dashboard;

public record MeetingDto(Guid Id, DateOnly ScheduledDate, string? TopicText);

public record AddTopicSuggestionRequest(string Text);

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
    string Period,
    string PeriodLabel,
    MeetingDto? CurrentMeeting,
    IReadOnlyList<TopicSuggestionDto> PendingSuggestions,
    IReadOnlyList<GroupSummaryDto> GroupSummaries,
    IReadOnlyList<WorkItemDto> DoneItems,
    IReadOnlyList<WorkItemDto> NotDoneItems,
    IReadOnlyList<WorkItemDto> AssignedItems);
