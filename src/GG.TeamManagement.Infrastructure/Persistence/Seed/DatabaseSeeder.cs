using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        await SeedGroupsAndMembersAsync(db);
        await SeedIdentityAsync(userManager, roleManager, configuration, logger);
    }

    private static async Task SeedGroupsAndMembersAsync(AppDbContext db)
    {
        if (!await db.Groups.AnyAsync())
        {
            db.Groups.AddRange(
                new Group { Id = SeedData.OfficeManagementGroupId, Name = "Office Management", IsOfficeManagementTeam = true },
                new Group { Id = SeedData.SubGroup1Id, Name = "Sub-Group 1", IsOfficeManagementTeam = false },
                new Group { Id = SeedData.SubGroup2Id, Name = "Sub-Group 2", IsOfficeManagementTeam = false },
                new Group { Id = SeedData.SubGroup3Id, Name = "Sub-Group 3", IsOfficeManagementTeam = false },
                new Group { Id = SeedData.SubGroup4Id, Name = "Sub-Group 4", IsOfficeManagementTeam = false });
        }

        if (!await db.Members.AnyAsync())
        {
            db.Members.AddRange(
                new Member { Id = SeedData.LeadMemberId, Name = "Office Lead", GroupId = SeedData.OfficeManagementGroupId, Role = MemberRole.Lead },
                new Member { Id = SeedData.OfficeMemberId, Name = "Office Member", GroupId = SeedData.OfficeManagementGroupId, Role = MemberRole.Member },
                new Member { Id = SeedData.SubGroup1MemberId, Name = "Sub-Group 1 Member", GroupId = SeedData.SubGroup1Id, Role = MemberRole.Member },
                new Member { Id = SeedData.SubGroup2MemberId, Name = "Sub-Group 2 Member", GroupId = SeedData.SubGroup2Id, Role = MemberRole.Member },
                new Member { Id = SeedData.SubGroup3MemberId, Name = "Sub-Group 3 Member", GroupId = SeedData.SubGroup3Id, Role = MemberRole.Member },
                new Member { Id = SeedData.SubGroup4MemberId, Name = "Sub-Group 4 Member", GroupId = SeedData.SubGroup4Id, Role = MemberRole.Member });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        foreach (var role in new[] { nameof(MemberRole.Lead), nameof(MemberRole.Member) })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var password = configuration["SEED_DEFAULT_PASSWORD"]
                       ?? configuration["Seed:DefaultPassword"];

        if (string.IsNullOrWhiteSpace(password) || password.Contains("<<", StringComparison.Ordinal))
        {
            logger.LogWarning("SEED_DEFAULT_PASSWORD is not configured; identity users were not seeded.");
            return;
        }

        await EnsureUserAsync(userManager, "lead@gg.local", password, SeedData.LeadMemberId, nameof(MemberRole.Lead));
        await EnsureUserAsync(userManager, "office.member@gg.local", password, SeedData.OfficeMemberId, nameof(MemberRole.Member));
        await EnsureUserAsync(userManager, "sg1.member@gg.local", password, SeedData.SubGroup1MemberId, nameof(MemberRole.Member));
        await EnsureUserAsync(userManager, "sg2.member@gg.local", password, SeedData.SubGroup2MemberId, nameof(MemberRole.Member));
        await EnsureUserAsync(userManager, "sg3.member@gg.local", password, SeedData.SubGroup3MemberId, nameof(MemberRole.Member));
        await EnsureUserAsync(userManager, "sg4.member@gg.local", password, SeedData.SubGroup4MemberId, nameof(MemberRole.Member));
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        Guid memberId,
        string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MemberId = memberId
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
    }
}
