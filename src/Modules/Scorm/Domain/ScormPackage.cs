using LibreLms.SharedKernel;

namespace LibreLms.Modules.Scorm.Domain;

/// <summary>
/// Represents an uploaded SCORM 1.2 package. One package is linked to one Course
/// in the Catalog module.
/// </summary>
public class ScormPackage : Entity<Guid>
{
    /// <summary>FK to the catalog course this package belongs to. Null = available pool (unassociated).</summary>
    public Guid? CourseId { get; set; }

    /// <summary>Title extracted from imsmanifest.xml.</summary>
    public string ManifestTitle { get; set; } = string.Empty;

    /// <summary>Relative path to the launch SCO's HTML file (e.g., "index.html").</summary>
    public string LaunchPath { get; set; } = string.Empty;

    /// <summary>Server-relative path to extracted content files (e.g., "scorm-content/{Id}").</summary>
    public string ContentDirectory { get; set; } = string.Empty;

    /// <summary>When the package was uploaded.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public ScormPackage()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
