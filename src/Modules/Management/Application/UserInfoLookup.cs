using LibreLms.Contracts.Management;
using EnrollmentIUserLookup = LibreLms.Contracts.Enrollment.IUserLookup;
using EnrollmentUserScopeInfo = LibreLms.Contracts.Enrollment.UserScopeInfo;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Implements the cross-module IUserInfoLookup contract.
/// Spec 027 (R9): delegates to the Enrollment module's IUserLookup contract —
/// this module no longer touches EnrollmentDbContext directly.
/// </summary>
public class UserInfoLookup(EnrollmentIUserLookup userLookup) : IUserInfoLookup
{
    public async Task<UserScopeInfo?> GetUserScopeAsync(Guid userId)
    {
        EnrollmentUserScopeInfo? scope = await userLookup.GetUserScopeAsync(userId);
        return scope is null ? null : new UserScopeInfo(scope.OrganizationId, scope.Role);
    }
}
