using Microsoft.AspNetCore.Identity;

namespace GG.TeamManagement.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid MemberId { get; set; }
}
