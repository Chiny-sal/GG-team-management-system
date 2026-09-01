using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Auth;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db,
        ITokenService tokens,
        ICurrentUser currentUser,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _currentUser = currentUser;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email)
            ?? await _userManager.FindByNameAsync(email);
        if (user is null)
        {
            _logger.LogWarning("Login failed: no AspNetUsers row for {Email} (FindByEmailAsync + FindByNameAsync).", email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning(
                "Login failed: UserManager.CheckPasswordAsync returned false for {Email} id={Id}.",
                email,
                user.Id);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == user.MemberId, cancellationToken);
        if (member is null)
            return Unauthorized(new { message = "Member profile is missing." });

        var token = _tokens.CreateToken(Guid.Parse(user.Id), user.Email!, member);
        return new AuthResponse(token, member.Id, member.GroupId, member.Name, member.Role, user.Email!);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken)
    {
        if (_currentUser.MemberId is null)
            return Unauthorized();

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == _currentUser.MemberId, cancellationToken);
        if (member is null) return Unauthorized();

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        return new CurrentUserDto(member.Id, member.GroupId, member.Name, member.Role, email);
    }
}
