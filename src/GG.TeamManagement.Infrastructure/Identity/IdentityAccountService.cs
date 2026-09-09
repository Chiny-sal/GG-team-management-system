using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Infrastructure.Identity;

public class IdentityAccountService : IIdentityAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityAccountService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string?> GetEmailAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.MemberId == memberId, cancellationToken);
        return user?.Email;
    }

    public async Task UpdateEmailAsync(Guid memberId, string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.MemberId == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Login account not found.");

        var normalized = email.Trim();
        var existing = await _userManager.FindByEmailAsync(normalized);
        if (existing is not null && existing.Id != user.Id)
            throw new InvalidOperationException("That email is already in use.");

        user.Email = normalized;
        user.UserName = normalized;
        user.NormalizedEmail = _userManager.NormalizeEmail(normalized);
        user.NormalizedUserName = _userManager.NormalizeName(normalized);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task ChangePasswordAsync(
        Guid memberId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.MemberId == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Login account not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task PromoteToLeadAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.MemberId == memberId, cancellationToken);
        if (user is null) return;

        if (!await _userManager.IsInRoleAsync(user, "Lead"))
        {
            var added = await _userManager.AddToRoleAsync(user, "Lead");
            if (!added.Succeeded)
                throw new InvalidOperationException(string.Join(" ", added.Errors.Select(e => e.Description)));
        }

        if (await _userManager.IsInRoleAsync(user, "Member"))
            await _userManager.RemoveFromRoleAsync(user, "Member");
    }
}
