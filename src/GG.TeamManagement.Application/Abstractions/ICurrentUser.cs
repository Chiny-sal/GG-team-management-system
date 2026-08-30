using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? MemberId { get; }
    Guid? GroupId { get; }
    MemberRole? Role { get; }
    string? Name { get; }
    bool IsLead { get; }
}
