using LearningLms.SharedKernel;

namespace LearningLms.Modules.Catalog.Domain;

/// <summary>A learnable unit of content in the catalog.</summary>
public class Course : Entity<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Course()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
