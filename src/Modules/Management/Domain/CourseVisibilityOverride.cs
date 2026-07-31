using LibreLms.SharedKernel;

namespace LibreLms.Modules.Management.Domain;

/// <summary>
/// Records an Organization Admin's decision to hide a specific inherited (parent) course
/// from their organization's visible catalog. Only applies to inherited courses,
/// not locally uploaded ones.
/// </summary>
public class CourseVisibilityOverride : Entity<Guid>
{
    /// <summary>The organization whose admin made the override.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The inherited course being hidden (or shown).</summary>
    public Guid CourseId { get; set; }

    /// <summary>Visibility state — true means the course is hidden from this organization.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Which admin created the override (nullable for system-created).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>When the override was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public CourseVisibilityOverride()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
