using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Members;

public record MemberProfileDto(
    Guid Id,
    string Name,
    string? Email,
    string? TelegramUsername,
    Guid? GroupId,
    string? GroupName,
    MemberRole? Role,
    bool IsSelf,
    bool CanViewDetails,
    bool CanEditProfile,
    bool CanChangePassword,
    bool CanSetAsLead,
    bool CanViewOtherGroupBoards,
    bool CanAssignWorkToOtherGroups,
    bool CanRevokeLead,
    bool CanDeleteMember);

public record UpdateMemberProfileRequest(
    string? Name,
    string? Email,
    string? TelegramUsername);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record SetLeadsRequest(
    IReadOnlyList<Guid> MemberIds,
    bool? CanViewOtherGroupBoards = null,
    bool? CanAssignWorkToOtherGroups = null);

public record SetCrossGroupBoardAccessRequest(bool CanViewOtherGroupBoards);

public record SetCrossGroupAssignmentAccessRequest(bool CanAssignWorkToOtherGroups);

public record MemberWorkSummaryDto(
    Guid Id,
    string Name,
    Guid GroupId,
    string GroupName,
    MemberRole Role,
    bool CanViewOtherGroupBoards,
    bool CanAssignWorkToOtherGroups,
    int AssignedCount,
    int OngoingCount,
    int DoneCount,
    int NotDoneCount,
    int TotalAssigned);
