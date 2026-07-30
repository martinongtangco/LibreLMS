using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Catalog.Domain;
using LibreLms.Modules.Catalog.Infrastructure;

namespace LibreLms.Modules.Catalog.Application;

/// <summary>Application service for browsing, searching, and retrieving courses.</summary>
public class CourseCatalogService(CatalogDbContext context)
{
    /// <summary>List all courses with optional search and category filters.</summary>
    public async Task<IEnumerable<Course>> ListAsync(string? search = null, string? category = null)
    {
        var query = context.Courses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(c => c.Title.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(c => c.Category == category);
        }

        return await query.OrderBy(c => c.Title).ToListAsync();
    }

    /// <summary>Get a single course by ID, or null if not found.</summary>
    public async Task<Course?> GetByIdAsync(Guid id)
    {
        return await context.Courses.FindAsync(id);
    }

    /// <summary>
    /// Get course details with enrollment status for a specific student.
    /// The enrollment status is determined by the Enrollment module, not this service.
    /// This method returns the course; the caller checks enrollment status separately.
    /// </summary>
    public async Task<Course?> GetCourseForDetailAsync(Guid id)
    {
        return await context.Courses.FindAsync(id);
    }

    /// <summary>Create a new course in the catalog.</summary>
    public async Task<Course> CreateAsync(Endpoints.CreateCourseRequest request)
    {
        var course = new Domain.Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            ShortDescription = request.ShortDescription,
            FullDescription = request.FullDescription,
            Category = request.Category,
            Duration = request.Duration,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return course;
    }
}
