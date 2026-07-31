using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Management.Domain;

namespace LibreLms.Modules.Management.Infrastructure;

/// <summary>EF Core context for the Management module — owns Organizations and CourseVisibilityOverrides tables.</summary>
public class ManagementDbContext(DbContextOptions<ManagementDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<CourseVisibilityOverride> CourseVisibilityOverrides => Set<CourseVisibilityOverride>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.IsDisabled)
                .IsRequired()
                .HasDefaultValue(false);

            // Self-referencing relationship (parent-child)
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: name must be unique within parent
            entity.HasIndex(e => new { e.Name, e.ParentId })
                .IsUnique();
        });

        builder.Entity<CourseVisibilityOverride>(entity =>
        {
            entity.ToTable("CourseVisibilityOverrides");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IsHidden)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // Unique constraint: one override per (OrganizationId, CourseId) pair
            entity.HasIndex(e => new { e.OrganizationId, e.CourseId })
                .IsUnique();
        });
    }
}
