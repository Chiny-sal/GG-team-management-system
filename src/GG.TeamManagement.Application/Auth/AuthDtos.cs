using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Auth;

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    Guid MemberId,
    Guid GroupId,
    string Name,
    MemberRole Role,
    string Email);

public record CurrentUserDto(
    Guid MemberId,
    Guid GroupId,
    string Name,
    MemberRole Role,
    string Email);
