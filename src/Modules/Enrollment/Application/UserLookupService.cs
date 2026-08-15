using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Read-only user facts for other modules (spec 027). In this system every account is a
/// Student row (the role string distinguishes privilege), so "learner counts" count
/// Students rows — matching the pre-existing dashboard semantics exactly.
/// </summary>
public sealed class UserLookupService : IUserLookup
{
    private readonly EnrollmentDbContext _context;

    public UserLookupService(EnrollmentDbContext context)
    {
        _context = context;
    }

    public async Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId)
    {
        var row = await _context.Students
            .Where(s => s.Id == studentId)
            .Select(s => new { s.OrganizationId, s.Roles })
            .FirstOrDefaultAsync();

        return row is null ? null : new UserScopeInfo(row.OrganizationId, row.Roles);
    }

    public async Task<int> CountLearnersAsync(Guid? organizationId = null)
    {
        return organizationId.HasValue
            ? await _context.Students.CountAsync(s => s.OrganizationId == organizationId.Value)
            : await _context.Students.CountAsync();
    }

    public async Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync()
    {
        return await _context.Students
            .GroupBy(s => s.OrganizationId)
            .Select(g => new OrgLearnerCount(g.Key, g.Count()))
            .ToListAsync();
    }

    public async Task<string?> GetUserNameAsync(Guid studentId)
    {
        return await _context.Students
            .Where(s => s.Id == studentId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds)
    {
        var ids = studentIds.ToList();
        return await _context.Students
            .Where(s => ids.Contains(s.Id))
            .Select(s => new UserSummary(s.Id, s.Name, s.Email))
            .ToListAsync();
    }

    public async Task<int> CountByRoleAsync(string role)
    {
        return await _context.Students.CountAsync(s => s.Roles == role);
    }
}
