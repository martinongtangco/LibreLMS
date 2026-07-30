using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Enrollment.Domain;

namespace LibreLms.Modules.Enrollment.Infrastructure;

/// <summary>EF Core context for the Enrollment module — owns Students and Enrollments tables.</summary>
public class EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<LibreLms.Modules.Enrollment.Domain.Enrollment> Enrollments => Set<LibreLms.Modules.Enrollment.Domain.Enrollment>();

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

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Roles)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });

        builder.Entity<LibreLms.Modules.Enrollment.Domain.Enrollment>(entity =>
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
