using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Notifications;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? MemberId,
    string? MemberName,
    Guid? WorkItemId,
    string? WorkItemTitle,
    DateTime CreatedAt,
    bool IsRead);

public record NotificationGroupDto(NotificationType Type, IReadOnlyList<NotificationDto> Items);
