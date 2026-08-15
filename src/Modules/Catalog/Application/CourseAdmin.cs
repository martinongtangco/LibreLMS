using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Modules.Catalog.Infrastructure;

namespace LibreLms.Modules.Catalog.Application;

/// <summary>
/// Implements the cross-module ICourseAdmin contract: catalog mutations other modules
/// (Management) may perform without reaching into Catalog's DbContext.
/// </summary>
public class CourseAdmin(CatalogDbContext context) : ICourseAdmin
{
    public async Task<bool> DeleteAsync(Guid courseId)
    {
        var course = await context.Courses.FindAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        context.Courses.Remove(course);
        await context.SaveChangesAsync();
        return true;
    }
}
