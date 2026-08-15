using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Catalog.Infrastructure;

namespace LibreLms.Modules.Catalog.Application;

/// <summary>
/// Implements the cross-module ICourseLookup contract.
/// Uses CatalogDbContext directly to fetch course summaries.
/// </summary>
public class CourseLookup(CatalogDbContext context) : ICourseLookup
{
    public async Task<CourseSummary?> GetCourseAsync(Guid courseId)
    {
        return await context.Courses
            .Where(c => c.Id == courseId)
            .Select(c => new CourseSummary(c.Id, c.Title, c.Category, c.OrganizationId))
            .FirstOrDefaultAsync();
    }

    public async Task<int> CountAsync()
    {
        return await context.Courses.CountAsync();
    }

    public async Task<int> CountByOrgAsync(Guid organizationId)
    {
        return await context.Courses.CountAsync(c => c.OrganizationId == organizationId);
    }

    public async Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.ToList();
        return await context.Courses
            .Where(c => ids.Contains(c.Id))
            .Select(c => new CourseSummary(c.Id, c.Title, c.Category, c.OrganizationId))
            .ToListAsync();
    }

    public async Task<IList<CourseSummary>> ListByOrgsAsync(IEnumerable<Guid> organizationIds)
    {
        var orgIds = organizationIds.ToList();
        return await context.Courses
            .Where(c => orgIds.Contains(c.OrganizationId))
            .Select(c => new CourseSummary(c.Id, c.Title, c.Category, c.OrganizationId))
            .ToListAsync();
    }

    public async Task<IList<CourseSummary>> ListAllAsync()
    {
        return await context.Courses
            .Select(c => new CourseSummary(c.Id, c.Title, c.Category, c.OrganizationId))
            .ToListAsync();
    }
}
