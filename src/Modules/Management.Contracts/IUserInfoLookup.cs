namespace LibreLms.Contracts.Management;

/// <summary>Result of a user scope lookup.</summary>
public record UserScopeInfo(Guid OrganizationId, string Role);

/// <summary>
/// Cross-module contract for looking up user role and organization information.
/// </summary>
public interface IUserInfoLookup
{
    /// <summary>Get the user primary organization ID and role. Returns null if not found.</summary>
    Task<UserScopeInfo?> GetUserScopeAsync(Guid userId);
}
