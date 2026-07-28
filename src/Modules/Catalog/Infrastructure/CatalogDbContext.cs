using Microsoft.EntityFrameworkCore;
using LearningLms.Modules.Catalog.Domain;

namespace LearningLms.Modules.Catalog.Infrastructure;

/// <summary>EF Core context for the Catalog module — owns the Courses table.</summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ShortDescription)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.FullDescription)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Duration)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });
    }
}
