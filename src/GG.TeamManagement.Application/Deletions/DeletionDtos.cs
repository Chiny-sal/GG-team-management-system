using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Deletions;

public record DeletionRequestDto(
    Guid Id,
    DeletionTargetType TargetType,
    Guid TargetId,
    string TargetName,
    Guid? RequestedByMemberId,
    string? RequestedByName,
    DateTime RequestedAt,
    DeletionRequestStatus Status,
    Guid? GroupId);
