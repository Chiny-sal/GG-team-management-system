using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Boards;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Domain;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Dashboard;

public class DashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentWeekService _currentWeek;

    public DashboardService(IApplicationDbContext db, ICurrentUser currentUser, ICurrentWeekService currentWeek)
    {
        _db = db;
        _currentUser = currentUser;
        _currentWeek = currentWeek;
    }

    public async Task<DashboardDto> GetAsync(string? period, CancellationToken cancellationToken = default)
    {
        var timePeriod = TimePeriodParser.Parse(period);
        var currentWeek = _currentWeek.GetCurrentWeekId();
        var (rangeStart, rangeEnd) = PeriodRange.For(timePeriod, currentWeek, DateTime.UtcNow);
        var meeting = await GetCurrentMeetingAsync(cancellationToken);

        var suggestions = _currentUser.IsLead
            ? await _db.TopicSuggestions.AsNoTracking()
                .Where(s => s.PromotedToMeetingId == null)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new TopicSuggestionDto(
                    s.Id,
                    s.SubmittedByTelegramUserId,
                    s.SubmittedByName,
                    s.Text,
                    s.SubmittedAt,
                    s.PromotedToMeetingId))
                .ToListAsync(cancellationToken)
            : [];

        var groups = await _db.Groups.AsNoTracking()
            .OrderByDescending(g => g.IsOfficeManagementTeam)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var items = await _db.WorkItems.AsNoTracking()
            .Include(w => w.AssignedMember)
            .Where(w => w.WeekId >= rangeStart && w.WeekId <= rangeEnd)
            .ToListAsync(cancellationToken);

        var summaries = groups.Select(g =>
        {
            var groupItems = items.Where(i => i.GroupId == g.Id).ToList();
            return new GroupSummaryDto(
                g.Id,
                g.Name,
                groupItems.Count(i => i.AssignedMemberId != null),
                groupItems.Count(i => i.Status == WorkItemStatus.Done),
                groupItems.Count(i => i.Status == WorkItemStatus.NotDone),
                groupItems.Count(i => i.Status == WorkItemStatus.Ongoing),
                groupItems.Count(i => i.Status == WorkItemStatus.NotAssigned));
        }).ToList();

        static bool IsIncompleteAssigned(WorkItem item) =>
            item.Status is WorkItemStatus.NotDone or WorkItemStatus.Assigned or WorkItemStatus.Ongoing;

        return new DashboardDto(
            TimePeriodParser.ToQuery(timePeriod),
            PeriodRange.Label(timePeriod, currentWeek, DateTime.UtcNow),
            meeting,
            suggestions,
            summaries,
            items.Where(i => i.Status == WorkItemStatus.Done).Select(WorkItemMapper.ToDto).ToList(),
            items.Where(IsIncompleteAssigned).Select(WorkItemMapper.ToDto).ToList(),
            items.Where(i => i.AssignedMemberId != null).Select(WorkItemMapper.ToDto).ToList());
    }

    public async Task<MeetingDto> PromoteSuggestionAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLead)
            throw new UnauthorizedAccessException("Only leads can promote topic suggestions.");

        var suggestion = await _db.TopicSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId, cancellationToken)
            ?? throw new KeyNotFoundException("Topic suggestion not found.");

        if (suggestion.PromotedToMeetingId is not null)
            throw new InvalidOperationException("This suggestion has already been promoted.");

        var meeting = await _db.Meetings
            .OrderByDescending(m => m.ScheduledDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (meeting is null)
        {
            meeting = new Meeting { ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow) };
            _db.Meetings.Add(meeting);
        }

        meeting.TopicText = suggestion.Text;
        suggestion.PromotedToMeetingId = meeting.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return new MeetingDto(meeting.Id, meeting.ScheduledDate, meeting.TopicText);
    }

    public async Task<TopicSuggestionDto> AddSuggestionAsync(AddTopicSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLead)
            throw new UnauthorizedAccessException("Only leads can add topic suggestions.");

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException("Topic text is required.");

        var suggestion = new TopicSuggestion
        {
            SubmittedByTelegramUserId = "manual",
            SubmittedByName = string.IsNullOrWhiteSpace(_currentUser.Name) ? "Lead" : _currentUser.Name,
            Text = request.Text.Trim(),
            SubmittedAt = DateTime.UtcNow
        };

        _db.TopicSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(cancellationToken);

        return new TopicSuggestionDto(
            suggestion.Id,
            suggestion.SubmittedByTelegramUserId,
            suggestion.SubmittedByName,
            suggestion.Text,
            suggestion.SubmittedAt,
            suggestion.PromotedToMeetingId);
    }

    private async Task<MeetingDto?> GetCurrentMeetingAsync(CancellationToken cancellationToken)
    {
        var meeting = await _db.Meetings.AsNoTracking()
            .OrderByDescending(m => m.ScheduledDate)
            .FirstOrDefaultAsync(cancellationToken);

        return meeting is null
            ? null
            : new MeetingDto(meeting.Id, meeting.ScheduledDate, meeting.TopicText);
    }
}
