using System.Globalization;
using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Application.Common;
using GG.TeamManagement.Application.Jobs;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GG.TeamManagement.Application.Attendance;

public class AttendanceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityFeedNotifier _feed;
    private readonly ITelegramNotifier _telegram;
    private readonly MissedLastTwoMeetingsCheckJob _missedTwo;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IActivityFeedNotifier feed,
        ITelegramNotifier telegram,
        MissedLastTwoMeetingsCheckJob missedTwo,
        ILogger<AttendanceService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _feed = feed;
        _telegram = telegram;
        _missedTwo = missedTwo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AttendanceRosterMemberDto>> GetRosterAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        return await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .OrderByDescending(m => m.Group.IsOfficeManagementTeam)
            .ThenBy(m => m.Group.Name)
            .ThenBy(m => m.Name)
            .Select(m => new AttendanceRosterMemberDto(
                m.Id,
                m.Name,
                m.Group.Name,
                m.TelegramUserId != null && m.TelegramUserId != ""))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceSessionSummaryDto>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        return await _db.MeetingAttendances.AsNoTracking()
            .Include(s => s.CreatedByMember)
            .Include(s => s.Records)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new AttendanceSessionSummaryDto(
                s.Id,
                s.Date,
                s.CreatedAt,
                s.CreatedByMember.Name,
                s.Records.Count(r => r.Present),
                s.Records.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceSessionDto> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);
        return await MapSessionAsync(id, cancellationToken);
    }

    public async Task<AttendanceSessionDto> CreateSessionAsync(
        SaveAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var actorId = _currentUser.MemberId
            ?? throw new UnauthorizedAccessException("Current member is required.");
        var actor = _currentUser.Name ?? "Someone";
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var questions = (request.Questions ?? [])
            .Select((text, index) => (Text: text.Trim(), Index: index))
            .Where(q => q.Text.Length > 0)
            .ToList();

        var session = new MeetingAttendance
        {
            Date = date,
            CreatedByMemberId = actorId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var question in questions)
        {
            session.CustomQuestions.Add(new CustomQuestion
            {
                QuestionText = question.Text.Length > 500 ? question.Text[..500] : question.Text,
                SortOrder = question.Index
            });
        }

        var members = await LoadMembersAsync(cancellationToken);
        var memberById = members.ToDictionary(m => m.Id);
        var rows = request.Rows ?? [];

        foreach (var row in rows)
        {
            if (!memberById.ContainsKey(row.MemberId)) continue;

            var record = new AttendanceRecord
            {
                MemberId = row.MemberId,
                Present = row.Present,
                AttendedWeeklyClass = row.AttendedWeeklyClass
            };

            var orderedQuestions = session.CustomQuestions.OrderBy(q => q.SortOrder).ToList();
            for (var i = 0; i < orderedQuestions.Count; i++)
            {
                record.Answers.Add(new AttendanceAnswer
                {
                    CustomQuestion = orderedQuestions[i],
                    Value = AnswerValue(row, orderedQuestions[i], i)
                });
            }

            session.Records.Add(record);
        }

        _db.MeetingAttendances.Add(session);

        var log = NewLog(
            session.Id,
            ChangeType.Created,
            $"{actor} took attendance for the {FormatMeetingDate(date)} meeting.");
        _db.ActivityLogEntries.Add(log);

        await _db.SaveChangesAsync(cancellationToken);
        await _missedTwo.ExecuteAsync(cancellationToken);
        await NotifyAsync(log, cancellationToken);

        return await MapSessionAsync(session.Id, cancellationToken);
    }

    public async Task<AttendanceSessionDto> UpdateSessionAsync(
        Guid id,
        UpdateAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var session = await _db.MeetingAttendances
            .Include(s => s.CustomQuestions)
            .Include(s => s.Records)
                .ThenInclude(r => r.Answers)
            .Include(s => s.Records)
                .ThenInclude(r => r.Member)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Attendance session not found.");

        var members = await LoadMembersAsync(cancellationToken);
        var memberById = members.ToDictionary(m => m.Id);
        var recordByMember = session.Records.ToDictionary(r => r.MemberId);
        var questions = session.CustomQuestions.OrderBy(q => q.SortOrder).ToList();
        var newlyPresent = new List<string>();
        var otherChanges = new List<string>();

        foreach (var row in request.Rows ?? [])
        {
            if (!memberById.TryGetValue(row.MemberId, out var member)) continue;

            if (!recordByMember.TryGetValue(row.MemberId, out var record))
            {
                record = new AttendanceRecord { MemberId = row.MemberId };
                session.Records.Add(record);
                recordByMember[row.MemberId] = record;
            }

            if (!record.Present && row.Present)
                newlyPresent.Add(member.Name);
            else if (record.Present && !row.Present)
                otherChanges.Add($"marked {member.Name} as not Present");

            if (record.AttendedWeeklyClass != row.AttendedWeeklyClass)
                otherChanges.Add(
                    row.AttendedWeeklyClass
                        ? $"marked {member.Name} as attended weekly class"
                        : $"cleared weekly class for {member.Name}");

            record.Present = row.Present;
            record.AttendedWeeklyClass = row.AttendedWeeklyClass;

            var answersByQuestion = record.Answers.ToDictionary(a => a.CustomQuestionId);
            for (var i = 0; i < questions.Count; i++)
            {
                var question = questions[i];
                var value = AnswerValue(row, question, i);
                if (answersByQuestion.TryGetValue(question.Id, out var answer))
                {
                    if (answer.Value != value)
                        otherChanges.Add($"updated '{question.QuestionText}' for {member.Name}");
                    answer.Value = value;
                }
                else
                {
                    record.Answers.Add(new AttendanceAnswer
                    {
                        CustomQuestionId = question.Id,
                        Value = value
                    });
                }
            }
        }

        var actor = _currentUser.Name ?? "Someone";
        var summary = BuildUpdateSummary(actor, session.Date, newlyPresent, otherChanges);
        var log = NewLog(session.Id, ChangeType.Updated, summary);
        _db.ActivityLogEntries.Add(log);

        await _db.SaveChangesAsync(cancellationToken);
        await _missedTwo.ExecuteAsync(cancellationToken);
        await NotifyAsync(log, cancellationToken);

        return await MapSessionAsync(session.Id, cancellationToken);
    }

    public async Task<BroadcastMessageResult> BroadcastAsync(
        BroadcastMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureOfficeManagementAsync(cancellationToken);

        var text = request.Message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Message is required.");

        var ids = (request.MemberIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one member.");

        var members = await _db.Members.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var sent = 0;
        var skipped = 0;
        var sentNames = new List<string>();

        foreach (var member in members.OrderBy(m => m.Name))
        {
            if (string.IsNullOrWhiteSpace(member.TelegramUserId))
            {
                skipped++;
                _logger.LogWarning(
                    "Skipping bot message for {MemberName}: no TelegramUserId.",
                    member.Name);
                continue;
            }

            await _telegram.SendDirectMessageAsync(
                member.TelegramUserId,
                text,
                "office management broadcast",
                member.Name,
                member.TelegramUsername,
                cancellationToken);
            sent++;
            sentNames.Add(member.Name);
        }

        var actor = _currentUser.Name ?? "Someone";
        var who = sentNames.Count == 0
            ? "0 members"
            : sentNames.Count <= 8
                ? string.Join(", ", sentNames)
                : $"{sentNames.Count} members";
        var skipNote = skipped > 0
            ? $" ({skipped} skipped — no Telegram)"
            : "";
        var log = NewLog(
            _currentUser.MemberId ?? Guid.Empty,
            ChangeType.Created,
            $"{actor} sent a bot message to {who}{skipNote}.");
        log.EntityType = "BroadcastMessage";
        _db.ActivityLogEntries.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
        await NotifyAsync(log, cancellationToken);

        return new BroadcastMessageResult(sent, skipped);
    }

    private async Task<AttendanceSessionDto> MapSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await _db.MeetingAttendances.AsNoTracking()
            .Include(s => s.CreatedByMember)
            .Include(s => s.CustomQuestions)
            .Include(s => s.Records)
                .ThenInclude(r => r.Answers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Attendance session not found.");

        var members = await LoadMembersAsync(cancellationToken);
        var recordByMember = session.Records.ToDictionary(r => r.MemberId);
        var questions = session.CustomQuestions
            .OrderBy(q => q.SortOrder)
            .Select(q => new AttendanceQuestionDto(q.Id, q.QuestionText, q.SortOrder))
            .ToList();

        var records = members.Select(member =>
        {
            recordByMember.TryGetValue(member.Id, out var record);
            var answers = questions.Select(q =>
            {
                var value = record?.Answers.FirstOrDefault(a => a.CustomQuestionId == q.Id)?.Value ?? false;
                return new AttendanceAnswerDto(q.Id, value);
            }).ToList();

            return new AttendanceRecordDto(
                member.Id,
                member.Name,
                member.Group.Name,
                record?.Present ?? false,
                record?.AttendedWeeklyClass ?? false,
                answers);
        }).ToList();

        return new AttendanceSessionDto(
            session.Id,
            session.Date,
            session.CreatedAt,
            session.CreatedByMember.Name,
            questions,
            records);
    }

    private async Task<List<Member>> LoadMembersAsync(CancellationToken cancellationToken) =>
        await _db.Members.AsNoTracking()
            .Include(m => m.Group)
            .OrderByDescending(m => m.Group.IsOfficeManagementTeam)
            .ThenBy(m => m.Group.Name)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

    private async Task EnsureOfficeManagementAsync(CancellationToken cancellationToken)
    {
        if (!await OfficeAccess.IsOfficeManagementAsync(_db, _currentUser, cancellationToken))
            throw new UnauthorizedAccessException("Only Office Management can access attendance.");
    }

    private ActivityLogEntry NewLog(Guid entityId, ChangeType changeType, string summary) =>
        new()
        {
            EntityType = "MeetingAttendance",
            EntityId = entityId,
            ChangeType = changeType,
            Summary = summary,
            ChangedByMemberId = _currentUser.MemberId,
            GroupId = _currentUser.GroupId,
            OccurredAt = DateTime.UtcNow
        };

    private async Task NotifyAsync(ActivityLogEntry log, CancellationToken cancellationToken)
    {
        await _feed.BroadcastAsync([log], cancellationToken);
        await _feed.NotifyEntitiesChangedAsync("MeetingAttendance", cancellationToken);
        await _feed.NotifyEntitiesChangedAsync("Notification", cancellationToken);
    }

    private static bool AnswerValue(SaveAttendanceRowRequest row, CustomQuestion question, int index)
    {
        if (row.Answers is null) return false;
        if (row.Answers.TryGetValue(question.Id.ToString(), out var byId)) return byId;
        if (row.Answers.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out var byIndex)) return byIndex;
        if (row.Answers.TryGetValue(question.QuestionText, out var byText)) return byText;
        return false;
    }

    private static string BuildUpdateSummary(
        string actor,
        DateOnly date,
        IReadOnlyList<string> newlyPresent,
        IReadOnlyList<string> otherChanges)
    {
        var dateLabel = FormatMeetingDate(date);
        var parts = new List<string>();
        if (newlyPresent.Count > 0)
            parts.Add($"marked {JoinNames(newlyPresent)} as Present");
        parts.AddRange(otherChanges.Take(6));

        if (parts.Count == 0)
            return $"{actor} updated attendance for {dateLabel}.";

        return $"{actor} updated attendance for {dateLabel}: {string.Join("; ", parts)}.";
    }

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    internal static string FormatMeetingDate(DateOnly date)
    {
        var day = date.Day;
        var suffix = (day % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            }
        };
        return $"{date.ToString("MMMM", CultureInfo.InvariantCulture)} {day}{suffix}";
    }
}
