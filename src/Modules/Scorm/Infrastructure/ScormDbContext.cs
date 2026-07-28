using Microsoft.EntityFrameworkCore;
using LearningLms.Modules.Scorm.Domain;

namespace LearningLms.Modules.Scorm.Infrastructure;

/// <summary>EF Core context for the Scorm module — owns ScormPackages and CourseAttempts tables.</summary>
public class ScormDbContext(DbContextOptions<ScormDbContext> options) : DbContext(options)
{
    public DbSet<ScormPackage> ScormPackages => Set<ScormPackage>();
    public DbSet<CourseAttempt> CourseAttempts => Set<CourseAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ScormPackage>(entity =>
        {
            entity.ToTable("ScormPackages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CourseId).IsRequired();
            entity.HasIndex(e => e.CourseId).IsUnique();

            entity.Property(e => e.ManifestTitle)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.LaunchPath).IsRequired();
            entity.Property(e => e.ContentDirectory).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        builder.Entity<CourseAttempt>(entity =>
        {
            entity.ToTable("CourseAttempts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.AttemptNumber).IsRequired();

            // Unique index on (StudentId, CourseId, AttemptNumber) for attempt sequencing
            entity.HasIndex(e => new { e.StudentId, e.CourseId, e.AttemptNumber })
                .IsUnique();

            // Index on (StudentId, CourseId) for querying latest attempt
            entity.HasIndex(e => new { e.StudentId, e.CourseId });

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.ScoreRaw);

            entity.Property(e => e.SessionTime)
                .HasMaxLength(10);

            entity.Property(e => e.SuspendData)
                .HasMaxLength(65536);

            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.LastCommitAt).IsRequired();
        });
    }
}
