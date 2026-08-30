using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GG.TeamManagement.Infrastructure.Identity;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(Guid userId, string email, Member member)
    {
        var key = _configuration["Jwt:Key"] ?? _configuration["JWT_KEY"];
        if (string.IsNullOrWhiteSpace(key) || key.Contains("<<", StringComparison.Ordinal))
            throw new InvalidOperationException("JWT_KEY / Jwt:Key is not configured.");

        var issuer = _configuration["Jwt:Issuer"] ?? "GG.TeamManagement";
        var audience = _configuration["Jwt:Audience"] ?? "GG.TeamManagement";
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 480;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, member.Name),
            new(ClaimTypes.Role, member.Role.ToString()),
            new("memberId", member.Id.ToString()),
            new("groupId", member.GroupId.ToString()),
            new("name", member.Name),
            new("role", member.Role.ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
