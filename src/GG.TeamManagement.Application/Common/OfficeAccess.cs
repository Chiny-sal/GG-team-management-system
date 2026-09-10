using GG.TeamManagement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Common;

public static class OfficeAccess
{
    public static async Task<bool> IsOfficeManagementAsync(
        IApplicationDbContext db,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        if (user.IsOfficeManagement) return true;
        if (user.MemberId is null) return false;

        return await db.Members
            .Where(m => m.Id == user.MemberId)
            .Select(m => m.Group.IsOfficeManagementTeam)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<bool> CanAdministerAsync(
        IApplicationDbContext db,
        ICurrentUser user,
        CancellationToken cancellationToken) =>
        user.IsLead || await IsOfficeManagementAsync(db, user, cancellationToken);
}
