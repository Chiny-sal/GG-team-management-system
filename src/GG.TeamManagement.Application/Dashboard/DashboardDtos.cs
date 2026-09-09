using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Dashboard;

public record MeetingDto(Guid Id, DateOnly ScheduledDate, string? TopicText, string? Notes);

public record MeetingWorkItemDto(Guid Id, string Title, string? AssignedMemberName, WorkItemStatus Status);

public record PastMeetingDto(
    Guid Id,
    DateOnly ScheduledDate,
    string? TopicText,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<MeetingWorkItemDto> WorkItems);

public record PastMeetingDayDto(
    DateOnly ScheduledDate,
    IReadOnlyList<PastMeetingDto> Meetings);

public record UpdateMeetingNotesRequest(string? Notes);

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
    IReadOnlyList<PastMeetingDayDto> PastMeetings,
    IReadOnlyList<TopicSuggestionDto> PendingSuggestions,
    IReadOnlyList<GroupSummaryDto> GroupSummaries,
    IReadOnlyList<WorkItemDto> DoneItems,
    IReadOnlyList<WorkItemDto> NotDoneItems,
    IReadOnlyList<WorkItemDto> AssignedItems);
