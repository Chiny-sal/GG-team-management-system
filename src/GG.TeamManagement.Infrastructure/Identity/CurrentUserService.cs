using System.Security.Claims;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace GG.TeamManagement.Infrastructure.Identity;

public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? MemberId => ParseGuid(User?.FindFirstValue("memberId"));

    public Guid? GroupId => ParseGuid(User?.FindFirstValue("groupId"));

    public MemberRole? Role =>
        Enum.TryParse<MemberRole>(User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role"), out var role)
            ? role
            : null;

    public string? Name => User?.FindFirstValue("name") ?? User?.FindFirstValue(ClaimTypes.Name);

    public bool IsLead => Role == MemberRole.Lead;

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;
}
