using LibreLms.SharedKernel;

namespace LibreLms.Modules.Enrollment.Domain;

/// <summary>A learner on the platform.</summary>
public class Student : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;

    /// <summary>Primary organization this user belongs to.</summary>
    public Guid OrganizationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Backs the Settings page's "Email notifications" toggle.</summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>Backs the Settings page's "Theme" selector. Stored/displayed only — dark-theme tokens do not exist yet.</summary>
    public string ThemePreference { get; set; } = "System";

    public Student()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
