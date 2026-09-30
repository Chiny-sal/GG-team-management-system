using GG.TeamManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Group> Groups { get; }
    DbSet<Member> Members { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<WeeklyBoardSnapshot> WeeklyBoardSnapshots { get; }
    DbSet<Meeting> Meetings { get; }
    DbSet<TopicSuggestion> TopicSuggestions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ActivityLogEntry> ActivityLogEntries { get; }
    DbSet<DeletionRequest> DeletionRequests { get; }
    DbSet<MeetingAttendance> MeetingAttendances { get; }
    DbSet<CustomQuestion> CustomQuestions { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<AttendanceAnswer> AttendanceAnswers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
