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

    /// <summary>Self-service sign-ups start unverified; admin-created and seeded accounts are verified (spec 027).</summary>
    public bool IsEmailVerified { get; set; } = true;

    /// <summary>Randomly assigned at account creation; rotated on password reset so all
    /// pre-existing sessions are invalidated (spec 027 / ADR-0006).</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 hex of the pending verification token; null when no verification is pending.</summary>
    public string? VerificationTokenHash { get; set; }

    /// <summary>Expiry of the pending verification link (24 hours from issue); null together with the token.</summary>
    public DateTimeOffset? VerificationTokenExpiresAt { get; set; }

    /// <summary>SHA-256 hex of the pending password-reset token; null when no reset is pending.</summary>
    public string? ResetTokenHash { get; set; }

    /// <summary>Expiry of the pending reset link (30 minutes from issue); null together with the token.</summary>
    public DateTimeOffset? ResetTokenExpiresAt { get; set; }

    /// <summary>URL path of the display photo (e.g. "/avatars/&lt;guid&gt;.png"); null = no photo
    /// (the UI renders an initials placeholder). Set/cleared only by the profile photo save (spec 030).</summary>
    public string? AvatarPath { get; set; }

    public Student()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
