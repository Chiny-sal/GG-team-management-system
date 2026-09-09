using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Notifications;

public enum AssignmentStatusLabel
{
    NoAssignment = 0,
    AssignedWork = 1
}

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? MemberId,
    string? MemberName,
    Guid? WorkItemId,
    string? WorkItemTitle,
    DateTime CreatedAt,
    bool IsRead,
    AssignmentStatusLabel? AssignmentStatus);

public record NotificationGroupDto(NotificationType Type, IReadOnlyList<NotificationDto> Items);

public record NotificationsPageDto(bool CanMarkRead, IReadOnlyList<NotificationGroupDto> Groups);
