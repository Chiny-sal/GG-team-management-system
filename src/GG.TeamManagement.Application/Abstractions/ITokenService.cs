using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;

namespace GG.TeamManagement.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(Guid userId, string email, Member member);
}
