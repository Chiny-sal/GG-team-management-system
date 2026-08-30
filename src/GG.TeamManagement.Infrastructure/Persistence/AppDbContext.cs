using GG.TeamManagement.Application.Abstractions;
using GG.TeamManagement.Domain.Entities;
using GG.TeamManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GG.TeamManagement.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WeeklyBoardSnapshot> WeeklyBoardSnapshots => Set<WeeklyBoardSnapshot>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<TopicSuggestion> TopicSuggestions => Set<TopicSuggestion>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLogEntry> ActivityLogEntries => Set<ActivityLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });

        builder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TelegramUserId).HasMaxLength(64);
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.HasIndex(e => new { e.GroupId, e.WeekId });
            entity.HasOne(e => e.Group)
                .WithMany(g => g.WorkItems)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedMember)
                .WithMany(m => m.AssignedWorkItems)
                .HasForeignKey(e => e.AssignedMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CreatedByMember)
                .WithMany()
                .HasForeignKey(e => e.CreatedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WeeklyBoardSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GroupId, e.WeekId }).IsUnique();
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Snapshots)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SavedByMember)
                .WithMany()
                .HasForeignKey(e => e.SavedByMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Meeting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TopicText).HasMaxLength(2000);
        });

        builder.Entity<TopicSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubmittedByTelegramUserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SubmittedByName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Text).HasMaxLength(2000).IsRequired();
            entity.HasOne(e => e.PromotedToMeeting)
                .WithMany(m => m.PromotedSuggestions)
                .HasForeignKey(e => e.PromotedToMeetingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Member)
                .WithMany(m => m.Notifications)
                .HasForeignKey(e => e.MemberId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkItem)
                .WithMany()
                .HasForeignKey(e => e.WorkItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ActivityLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Summary).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.OccurredAt);
            entity.HasOne(e => e.ChangedByMember)
                .WithMany()
                .HasForeignKey(e => e.ChangedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
