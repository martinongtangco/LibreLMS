namespace LibreLms.Contracts.Enrollment;

/// <summary>Create and maintain platform users (accounts) from other modules.</summary>
public interface IUserProvisioning
{
    /// <summary>Create an account. Enforces the strict password policy and case-insensitive
    /// email uniqueness. Throws ArgumentException for invalid name/email/password policy
    /// failures and InvalidOperationException for duplicate email.
    /// <paramref name="isVerified"/>: admin-created = true; self-service sign-up = false.</summary>
    Task<StudentProvisionedDto> CreateAsync(string name, string email, string password,
        string role, Guid organizationId, bool isVerified);

    Task<StudentProvisionedDto?> GetByIdAsync(Guid studentId);

    /// <summary>List accounts in one organization (optionally filtered by exact role).</summary>
    Task<IList<StudentProvisionedDto>> ListByOrgAsync(Guid orgId, string? roleFilter = null);

    /// <summary>List all platform accounts (optionally filtered by exact role). SuperUser-scope listing.</summary>
    Task<IList<StudentProvisionedDto>> ListAsync(string? roleFilter = null);

    /// <summary>Update an account. Null arguments mean "no change" for that field.
    /// <paramref name="avatarPath"/> is the display photo's URL path (null/empty = no change, spec 030).</summary>
    Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role, Guid? organizationId,
        string? avatarPath = null);
    Task DeleteAsync(Guid studentId);

    /// <summary>Case-insensitive existence check (email is normalized before comparison).</summary>
    Task<bool> ExistsByEmailAsync(string email);
}

/// <summary>Minimal account data exposed across module boundaries (never includes the credential).
/// <c>AvatarPath</c> is the display photo's URL path (e.g. "/avatars/&lt;guid&gt;.png") or null (spec 030).</summary>
public record StudentProvisionedDto(Guid Id, string Name, string Email, string Role,
    Guid OrganizationId, DateTimeOffset CreatedAt, bool IsEmailVerified, string? AvatarPath = null);
