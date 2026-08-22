namespace LibreLms.Contracts.Enrollment;

/// <summary>Read-only user facts other modules need (no account mutation).</summary>
public interface IUserLookup
{
    /// <summary>Primary organization + role of a user, or null if the user does not exist.</summary>
    Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId);

    /// <summary>Count of platform accounts. In this system every account is a Student row
    /// (role distinguishes privilege), so this counts Students, optionally scoped to one org.</summary>
    Task<int> CountLearnersAsync(Guid? organizationId = null);

    /// <summary>Account count per organization (all orgs that have at least one account).</summary>
    Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync();

    /// <summary>Display name of a user, or null if not found.</summary>
    Task<string?> GetUserNameAsync(Guid studentId);

    /// <summary>Batch lookup of minimal user facts (names/emails) for display in other modules.</summary>
    Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds);

    /// <summary>Number of accounts holding the given exact role string.</summary>
    Task<int> CountByRoleAsync(string role);
}

/// <summary>A user's primary organization and role.</summary>
public record UserScopeInfo(Guid OrganizationId, string Role);

/// <summary>Account count for one organization.</summary>
public record OrgLearnerCount(Guid OrganizationId, int Count);

/// <summary>Minimal user identity for display across module boundaries; carries the user's
/// organization id so consuming modules can resolve org names in bounded batches.</summary>
public record UserSummary(Guid Id, string Name, string Email, Guid OrganizationId);
