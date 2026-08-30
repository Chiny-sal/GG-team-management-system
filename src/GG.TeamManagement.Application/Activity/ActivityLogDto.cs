using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Activity;

public record ActivityLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    ChangeType ChangeType,
    string Summary,
    Guid? ChangedByMemberId,
    string? ChangedByName,
    DateTime OccurredAt);
