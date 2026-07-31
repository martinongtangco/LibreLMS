using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>
/// Implements the cross-module IUserInfoLookup contract.
/// Uses EnrollmentDbContext to look up user role and primary organization.
/// </summary>
public class UserInfoLookup(EnrollmentDbContext context) : IUserInfoLookup
{
    public async Task<UserScopeInfo?> GetUserScopeAsync(Guid userId)
    {
        var student = await context.Students
            .Where(s => s.Id == userId)
            .Select(s => new { s.OrganizationId, s.Roles })
            .FirstOrDefaultAsync();

        if (student is null)
            return null;

        return new UserScopeInfo(student.OrganizationId, student.Roles);
    }
}
