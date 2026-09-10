using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Members;

public class MemberService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityAccountService _accounts;

    public MemberService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IIdentityAccountService accounts)
    {
        _db = db;
        _currentUser = currentUser;
        _accounts = accounts;
    }

    public async Task<MemberProfileDto> GetProfileAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        var isSelf = _currentUser.MemberId == memberId;
        var isOffice = await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken);
        var canViewDetails = isSelf || isOffice;

        if (!canViewDetails)
        {
            return new MemberProfileDto(
                member.Id,
                member.Name,
                null,
                null,
                null,
                null,
                null,
                isSelf,
                false,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        var canManageLead = isOffice && !isSelf;
        var email = await _accounts.GetEmailAsync(member.Id, cancellationToken);
        return new MemberProfileDto(
            member.Id,
            member.Name,
            email,
            member.TelegramUsername,
            member.GroupId,
            member.Group.Name,
            member.Role,
            isSelf,
            true,
            true,
            isSelf,
            canManageLead,
            member.CanViewOtherGroupBoards,
            member.CanAssignWorkToOtherGroups,
            canManageLead && member.Role == MemberRole.Lead);
    }

    public async Task<MemberProfileDto> UpdateProfileAsync(
        Guid memberId,
        UpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var isSelf = _currentUser.MemberId == memberId;
        var isOffice = await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken);
        if (!isSelf && !isOffice)
            throw new UnauthorizedAccessException("Only the account owner or Office Management can edit this profile.");

        var member = await _db.Members
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Name is required.");
            member.Name = name;
        }

        if (request.TelegramUsername is not null)
            member.TelegramUsername = TelegramHandle.Normalize(request.TelegramUsername);

        var emailChanged = false;
        if (request.Email is not null)
        {
            var email = request.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email is required.");
            var currentEmail = await _accounts.GetEmailAsync(member.Id, cancellationToken);
            if (!string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                await _accounts.UpdateEmailAsync(member.Id, email, cancellationToken);
                emailChanged = true;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (emailChanged)
        {
            var actor = _currentUser.Name ?? "Someone";
            _db.ActivityLogEntries.Add(new ActivityLogEntry
            {
                EntityType = "Member",
                EntityId = member.Id,
                ChangeType = ChangeType.Updated,
                Summary = $"{actor} updated the login email for '{member.Name}'.",
                ChangedByMemberId = _currentUser.MemberId,
                GroupId = member.GroupId,
                OccurredAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await GetProfileAsync(memberId, cancellationToken);
    }

    public async Task ChangePasswordAsync(
        Guid memberId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.MemberId != memberId)
            throw new UnauthorizedAccessException("Only the account owner can change this password.");

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            throw new InvalidOperationException("Current and new passwords are required.");

        await _accounts.ChangePasswordAsync(memberId, request.CurrentPassword, request.NewPassword, cancellationToken);
    }

    public async Task SetAsLeadsAsync(SetLeadsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var ids = request.MemberIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one member.");

        var members = await _db.Members
            .Include(m => m.Group)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);

        if (members.Count != ids.Count)
            throw new KeyNotFoundException("One or more members were not found.");

        await _db.Groups.Where(g => members.Select(m => m.GroupId).Contains(g.Id)).ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            if (member.Role == MemberRole.Lead) continue;
            member.Role = MemberRole.Lead;
            await _accounts.PromoteToLeadAsync(member.Id, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (request.CanViewOtherGroupBoards == true)
        {
            var granted = false;
            foreach (var member in members)
            {
                if (member.CanViewOtherGroupBoards) continue;
                member.CanViewOtherGroupBoards = true;
                granted = true;
            }

            if (granted)
                await _db.SaveChangesAsync(cancellationToken);
        }

        if (request.CanAssignWorkToOtherGroups == true)
        {
            var granted = false;
            foreach (var member in members)
            {
                if (member.CanAssignWorkToOtherGroups) continue;
                member.CanAssignWorkToOtherGroups = true;
                granted = true;
            }

            if (granted)
                await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeLeadAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        EnsureNotSelf(memberId);

        var member = await _db.Members
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        if (member.Role != MemberRole.Lead)
            throw new InvalidOperationException($"{member.Name} is not a Team Lead.");

        member.Role = MemberRole.Member;
        await _accounts.DemoteToMemberAsync(member.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetCrossGroupBoardAccessAsync(
        Guid memberId,
        SetCrossGroupBoardAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        EnsureNotSelf(memberId);

        var member = await _db.Members
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        if (member.CanViewOtherGroupBoards == request.CanViewOtherGroupBoards)
            return;

        member.CanViewOtherGroupBoards = request.CanViewOtherGroupBoards;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetCrossGroupAssignmentAccessAsync(
        Guid memberId,
        SetCrossGroupAssignmentAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        EnsureNotSelf(memberId);

        var member = await _db.Members
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        if (member.CanAssignWorkToOtherGroups == request.CanAssignWorkToOtherGroups)
            return;

        member.CanAssignWorkToOtherGroups = request.CanAssignWorkToOtherGroups;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberWorkSummaryDto>> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCanViewDirectoryAsync(cancellationToken);

        var members = await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .OrderByDescending(m => m.Group.IsOfficeManagementTeam)
            .ThenBy(m => m.Group.Name)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var counts = await _db.WorkItems.AsNoTracking()
            .Where(w => w.AssignedMemberId != null)
            .GroupBy(w => new { w.AssignedMemberId, w.Status })
            .Select(g => new { MemberId = g.Key.AssignedMemberId!.Value, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byMember = counts
            .GroupBy(c => c.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return members.Select(member =>
        {
            byMember.TryGetValue(member.Id, out var rows);
            int Count(WorkItemStatus status) => rows?.Where(r => r.Status == status).Sum(r => r.Count) ?? 0;
            var assigned = Count(WorkItemStatus.Assigned);
            var ongoing = Count(WorkItemStatus.Ongoing);
            var done = Count(WorkItemStatus.Done);
            var notDone = Count(WorkItemStatus.NotDone);
            return new MemberWorkSummaryDto(
                member.Id,
                member.Name,
                member.GroupId,
                member.Group.Name,
                member.Role,
                member.CanViewOtherGroupBoards,
                member.CanAssignWorkToOtherGroups,
                assigned,
                ongoing,
                done,
                notDone,
                assigned + ongoing + done + notDone);
        }).ToList();
    }

    public async Task EnsureCanViewDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsLead) return;
        if (await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken)) return;
        throw new UnauthorizedAccessException("Only Team Leads and Office Management can view the members directory.");
    }

    private async Task EnsureOfficeManagementAsync(CancellationToken cancellationToken)
    {
        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            throw new UnauthorizedAccessException("Only Office Management can manage Team Lead status and member permissions.");
    }

    private void EnsureNotSelf(Guid memberId)
    {
        if (_currentUser.MemberId == memberId)
            throw new InvalidOperationException("You cannot change your own Team Lead status or member permissions.");
    }
}
