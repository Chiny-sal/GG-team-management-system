using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

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
        await SeedGroupsAsync(db, logger);
        await SeedMembersAsync(db, logger);
        await SeedIdentityAsync(userManager, roleManager, configuration, logger);
    }

    private static async Task SeedGroupsAsync(AppDbContext db, ILogger logger)
    {
        Group[] groups =
        [
            new() { Id = SeedData.OfficeManagementGroupId, Name = "Office Management", IsOfficeManagementTeam = true },
            new() { Id = SeedData.SubGroup1Id, Name = "Sub-Group 1", IsOfficeManagementTeam = false },
            new() { Id = SeedData.SubGroup2Id, Name = "Sub-Group 2", IsOfficeManagementTeam = false },
            new() { Id = SeedData.SubGroup3Id, Name = "Sub-Group 3", IsOfficeManagementTeam = false },
            new() { Id = SeedData.SubGroup4Id, Name = "Sub-Group 4", IsOfficeManagementTeam = false }
        ];

        foreach (var group in groups)
            await InsertIfMissingAsync(db, db.Groups, group, g => g.Id == group.Id, logger, $"group '{group.Name}'");
    }

    private static async Task SeedMembersAsync(AppDbContext db, ILogger logger)
    {
        Member[] members =
        [
            new() { Id = SeedData.LeadMemberId, Name = "Office Lead", GroupId = SeedData.OfficeManagementGroupId, Role = MemberRole.Lead },
            new() { Id = SeedData.OfficeMemberId, Name = "Office Member", GroupId = SeedData.OfficeManagementGroupId, Role = MemberRole.Member },
            new() { Id = SeedData.SubGroup1MemberId, Name = "Sub-Group 1 Member", GroupId = SeedData.SubGroup1Id, Role = MemberRole.Member },
            new() { Id = SeedData.SubGroup2MemberId, Name = "Sub-Group 2 Member", GroupId = SeedData.SubGroup2Id, Role = MemberRole.Member },
            new() { Id = SeedData.SubGroup3MemberId, Name = "Sub-Group 3 Member", GroupId = SeedData.SubGroup3Id, Role = MemberRole.Member },
            new() { Id = SeedData.SubGroup4MemberId, Name = "Sub-Group 4 Member", GroupId = SeedData.SubGroup4Id, Role = MemberRole.Member }
        ];

        foreach (var member in members)
            await InsertIfMissingAsync(db, db.Members, member, m => m.Id == member.Id, logger, $"member '{member.Name}'");
    }

    private static async Task SeedIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        foreach (var role in new[] { nameof(MemberRole.Lead), nameof(MemberRole.Member) })
            await EnsureRoleAsync(roleManager, role, logger);

        var password = AppEnvironment.Optional(configuration, AppEnvironment.SeedDefaultPassword);
        if (password is null)
        {
            logger.LogWarning("SEED_DEFAULT_PASSWORD is not configured; identity users were not seeded.");
            return;
        }

        await EnsureUserAsync(userManager, "lead@gg.local", password, SeedData.LeadMemberId, nameof(MemberRole.Lead), logger);
        await EnsureUserAsync(userManager, "office.member@gg.local", password, SeedData.OfficeMemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg1.member@gg.local", password, SeedData.SubGroup1MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg2.member@gg.local", password, SeedData.SubGroup2MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg3.member@gg.local", password, SeedData.SubGroup3MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg4.member@gg.local", password, SeedData.SubGroup4MemberId, nameof(MemberRole.Member), logger);
    }

    private static async Task InsertIfMissingAsync<T>(
        AppDbContext db,
        DbSet<T> set,
        T entity,
        System.Linq.Expressions.Expression<Func<T, bool>> alreadyExists,
        ILogger logger,
        string label) where T : class
    {
        if (await set.AsNoTracking().AnyAsync(alreadyExists))
            return;

        set.Add(entity);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation("Seed skipped {Label}: already exists (unique constraint).", label);
            db.ChangeTracker.Clear();
        }
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string role, ILogger logger)
    {
        try
        {
            if (await roleManager.RoleExistsAsync(role))
                return;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (result.Succeeded || IsDuplicateIdentity(result))
                return;

            throw new InvalidOperationException($"Failed to seed role {role}: {Describe(result)}");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation("Seed skipped role '{Role}': already exists (unique constraint).", role);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        Guid memberId,
        string role,
        ILogger logger)
    {
        try
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    MemberId = memberId
                };

                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded && !IsDuplicateIdentity(created))
                    throw new InvalidOperationException($"Failed to seed user {email}: {Describe(created)}");

                existing = await userManager.FindByEmailAsync(email);
            }

            if (existing is null)
                return;

            if (!await userManager.IsInRoleAsync(existing, role))
            {
                var added = await userManager.AddToRoleAsync(existing, role);
                if (!added.Succeeded && !IsDuplicateIdentity(added))
                    throw new InvalidOperationException($"Failed to add {email} to role {role}: {Describe(added)}");
            }
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation("Seed skipped user '{Email}': already exists (unique constraint).", email);
        }
    }

    private static bool IsDuplicateIdentity(IdentityResult result) =>
        result.Errors.Any(error => error.Code is
            "DuplicateRoleName" or
            "DuplicateUserName" or
            "DuplicateEmail" or
            "UserAlreadyInRole");

    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));

    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;

            if (current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("23505", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
