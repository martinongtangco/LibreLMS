using Microsoft.EntityFrameworkCore;
using LearningLms.Modules.Enrollment.Domain;

namespace LearningLms.Modules.Enrollment.Infrastructure;

/// <summary>EF Core context for the Enrollment module — owns Students and Enrollments tables.</summary>
public class EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<LearningLms.Modules.Enrollment.Domain.Enrollment> Enrollments => Set<LearningLms.Modules.Enrollment.Domain.Enrollment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(320);

            // Unique email constraint
            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });

        builder.Entity<LearningLms.Modules.Enrollment.Domain.Enrollment>(entity =>
        {
            entity.ToTable("Enrollments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.EnrolledAt).IsRequired();

            // FR-005: Prevent duplicate enrollment at the database level
            entity.HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();
        });
    }
}
