namespace GG.TeamManagement.Application.Abstractions;

public interface IIdentityAccountService
{
    Task<string?> GetEmailAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task UpdateEmailAsync(Guid memberId, string email, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        Guid memberId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task PromoteToLeadAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task DemoteToMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    Task DeleteLoginAsync(Guid memberId, CancellationToken cancellationToken = default);
}
