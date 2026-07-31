using LibreLms.SharedKernel;

namespace LibreLms.Modules.Management.Domain;

/// <summary>
/// Represents a node in the organizational hierarchy. Every organization except the root
/// has exactly one parent. An organization can have zero or more child organizations
/// and can host its own courses.
/// </summary>
public class Organization : Entity<Guid>
{
    /// <summary>Display name for the organization. Unique within its parent.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Parent organization. Null for the root organization.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Navigation: parent organization (null for root).</summary>
    public Organization? Parent { get; set; }

    /// <summary>Navigation: child organizations.</summary>
    public ICollection<Organization> Children { get; set; } = new List<Organization>();

    /// <summary>When the organization was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; set; }

    public Organization()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }
}
