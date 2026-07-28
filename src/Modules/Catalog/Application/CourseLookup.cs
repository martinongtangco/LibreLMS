using Microsoft.EntityFrameworkCore;
using LearningLms.Contracts.Catalog;
using LearningLms.Modules.Catalog.Infrastructure;

namespace LearningLms.Modules.Catalog.Application;

/// <summary>
/// Implements the cross-module ICourseLookup contract.
/// Uses CatalogDbContext directly to fetch course summaries.
/// </summary>
public class CourseLookup(CatalogDbContext context) : ICourseLookup
{
    public async Task<CourseSummary?> GetCourseAsync(Guid courseId)
    {
        var course = await context.Courses
            .Where(c => c.Id == courseId)
            .Select(c => new CourseSummary(c.Id, c.Title))
            .FirstOrDefaultAsync();

        return course;
    }
}
