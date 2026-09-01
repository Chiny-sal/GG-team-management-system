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
        logger.LogInformation("Identity seed starting. AspNetUsers count={Count}.", await userManager.Users.CountAsync());

        foreach (var role in new[] { nameof(MemberRole.Lead), nameof(MemberRole.Member) })
            await EnsureRoleAsync(roleManager, role, logger);

        var fromEnv = Environment.GetEnvironmentVariable(AppEnvironment.SeedDefaultPassword);
        var fromConfig = configuration[AppEnvironment.SeedDefaultPassword];
        var password = AppEnvironment.Optional(configuration, AppEnvironment.SeedDefaultPassword);
        var source = !IsUsableSecret(fromEnv) ? (!IsUsableSecret(fromConfig) ? "none" : "configuration") : "environment";

        logger.LogInformation(
            "SEED_DEFAULT_PASSWORD source={Source}; env={EnvMasked} len={EnvLen}; config={ConfigMasked} len={ConfigLen}; used={UsedMasked} len={UsedLen}.",
            source,
            MaskSecret(fromEnv),
            fromEnv?.Length ?? 0,
            MaskSecret(fromConfig),
            fromConfig?.Length ?? 0,
            MaskSecret(password),
            password?.Length ?? 0);

        if (password is null)
        {
            throw new InvalidOperationException(
                "SEED_DEFAULT_PASSWORD is missing or still the <<placeholder>>. Set it in run-backend.ps1 or launchSettings.json. Identity users were not created.");
        }

        await EnsureUserAsync(userManager, "lead@gg.local", password, SeedData.LeadMemberId, nameof(MemberRole.Lead), logger);
        await EnsureUserAsync(userManager, "office.member@gg.local", password, SeedData.OfficeMemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg1.member@gg.local", password, SeedData.SubGroup1MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg2.member@gg.local", password, SeedData.SubGroup2MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg3.member@gg.local", password, SeedData.SubGroup3MemberId, nameof(MemberRole.Member), logger);
        await EnsureUserAsync(userManager, "sg4.member@gg.local", password, SeedData.SubGroup4MemberId, nameof(MemberRole.Member), logger);

        var lead = await FindSeedUserAsync(userManager, "lead@gg.local");
        logger.LogInformation(
            "Identity seed finished. AspNetUsers count={Count}; lead@gg.local persisted={Persisted}.",
            await userManager.Users.CountAsync(),
            lead is not null);

        if (lead is null)
        {
            throw new InvalidOperationException(
                "Identity seed did not persist lead@gg.local. Check SEED_DEFAULT_PASSWORD (env vs appsettings placeholder) and AspNetUsers.");
        }
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
            var existing = await FindSeedUserAsync(userManager, email);
            if (existing is null)
            {
                logger.LogInformation("Identity seed [{Email}]: not found by email or username; creating.", email);

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

                if (created.Succeeded)
                    logger.LogInformation("Identity seed [{Email}]: CreateAsync succeeded (hasher=UserManager.CreateAsync).", email);
                else
                    logger.LogInformation("Identity seed [{Email}]: CreateAsync reported duplicate; re-fetching.", email);

                existing = await FindSeedUserAsync(userManager, email);
            }
            else
            {
                logger.LogInformation(
                    "Identity seed [{Email}]: found existing id={Id} userName={UserName} normalizedEmail={NormalizedEmail} memberId={MemberId}.",
                    email,
                    existing.Id,
                    existing.UserName,
                    existing.NormalizedEmail,
                    existing.MemberId);
            }

            if (existing is null)
            {
                logger.LogError("Identity seed [{Email}]: still missing after create; cannot compare or reset password.", email);
                return;
            }

            var passwordMatches = await userManager.CheckPasswordAsync(existing, password);
            logger.LogInformation(
                "Identity seed [{Email}]: compared password via UserManager.CheckPasswordAsync; matches={Matches}.",
                email,
                passwordMatches);

            if (!passwordMatches)
            {
                logger.LogInformation(
                    "Identity seed [{Email}]: resetting password via UserManager.RemovePasswordAsync + AddPasswordAsync.",
                    email);

                if (await userManager.HasPasswordAsync(existing))
                {
                    var removed = await userManager.RemovePasswordAsync(existing);
                    if (!removed.Succeeded)
                        throw new InvalidOperationException($"Failed to clear password for {email}: {Describe(removed)}");
                }

                var addedPassword = await userManager.AddPasswordAsync(existing, password);
                if (!addedPassword.Succeeded)
                    throw new InvalidOperationException($"Failed to set password for {email}: {Describe(addedPassword)}");

                var verified = await userManager.CheckPasswordAsync(existing, password);
                logger.LogInformation(
                    "Identity seed [{Email}]: password reset complete; post-reset CheckPasswordAsync={Verified}.",
                    email,
                    verified);

                if (!verified)
                {
                    throw new InvalidOperationException(
                        $"Password for {email} was written but UserManager.CheckPasswordAsync still failed.");
                }
            }
            else
            {
                logger.LogInformation("Identity seed [{Email}]: password already matches SEED_DEFAULT_PASSWORD; not reset.", email);
            }

            if (!await userManager.IsInRoleAsync(existing, role))
            {
                var added = await userManager.AddToRoleAsync(existing, role);
                if (!added.Succeeded && !IsDuplicateIdentity(added))
                    throw new InvalidOperationException($"Failed to add {email} to role {role}: {Describe(added)}");
                logger.LogInformation("Identity seed [{Email}]: added to role {Role}.", email, role);
            }
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(
                "Identity seed [{Email}]: unique constraint during create; re-fetching to compare/reset password.",
                email);
            var existing = await FindSeedUserAsync(userManager, email);
            if (existing is null)
            {
                logger.LogError("Identity seed [{Email}]: unique constraint fired but user still not found.", email);
                return;
            }

            if (!await userManager.CheckPasswordAsync(existing, password))
            {
                if (await userManager.HasPasswordAsync(existing))
                    await userManager.RemovePasswordAsync(existing);
                var addedPassword = await userManager.AddPasswordAsync(existing, password);
                if (!addedPassword.Succeeded)
                    throw new InvalidOperationException($"Failed to set password for {email}: {Describe(addedPassword)}");
                logger.LogInformation("Identity seed [{Email}]: password reset after unique-constraint retry.", email);
            }
        }
    }

    private static async Task<ApplicationUser?> FindSeedUserAsync(UserManager<ApplicationUser> userManager, string email) =>
        await userManager.FindByEmailAsync(email) ?? await userManager.FindByNameAsync(email);

    private static bool IsUsableSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("<<", StringComparison.Ordinal);

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        if (value.Contains("<<", StringComparison.Ordinal))
            return "(placeholder)";
        if (value.Length == 1)
            return $"{value[0]}***";
        return $"{value[0]}{new string('*', value.Length - 2)}{value[^1]}";
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
