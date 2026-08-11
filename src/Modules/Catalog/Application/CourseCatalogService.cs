using System.Data;
using Microsoft.Data.SqlClient;
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
            OrganizationId = request.OrganizationId ?? Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return course;
    }

    /// <summary>Create a new course in the catalog, associated with an organization.</summary>
    public async Task<Course> CreateAsync(string title, string shortDescription, string fullDescription, string category, string duration, Guid organizationId)
    {
        var course = new Domain.Course
        {
            Id = Guid.NewGuid(),
            Title = title,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
            Category = category,
            Duration = duration,
            OrganizationId = organizationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return course;
    }

    /// <summary>Update an existing course in the catalog.</summary>
    public async Task<Course> UpdateAsync(Guid courseId, Endpoints.UpdateCourseRequest request)
    {
        var course = await context.Courses.FindAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException($"Course with ID {courseId} not found.");

        course.Title = request.Title;
        course.ShortDescription = request.ShortDescription;
        course.FullDescription = request.FullDescription;
        course.Category = request.Category;
        course.Duration = request.Duration;

        await context.SaveChangesAsync();

        return course;
    }

    /// <summary>List courses scoped to specific organization IDs (used for org-scoped queries).</summary>
    public async Task<IEnumerable<Course>> ListByOrgIdsAsync(IList<Guid> orgIds)
    {
        return await context.Courses
            .Where(c => orgIds.Contains(c.OrganizationId))
            .OrderBy(c => c.Title)
            .ToListAsync();
    }

    /// <summary>List courses owned by a specific organization.</summary>
    public async Task<IEnumerable<Course>> ListByOrganizationAsync(Guid orgId)
    {
        return await context.Courses
            .Where(c => c.OrganizationId == orgId)
            .OrderBy(c => c.Title)
            .ToListAsync();
    }

    /// <summary>
    /// Browse courses with search, category filter, and pagination using a T-SQL stored procedure.
    /// Uses SQL Server Full-Text Search (FTS) when available, falling back to LIKE-based search.
    /// </summary>
    public async Task<BrowseResult> BrowseAsync(
        string? searchTerm,
        string? category,
        int pageNumber,
        int pageSize,
        HashSet<Guid>? visibleCourseIds = null)
    {
        // Trim whitespace from search term
        searchTerm = searchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
            searchTerm = null;

        if (string.IsNullOrWhiteSpace(category))
            category = null;

        if (pageNumber < 1)
            pageNumber = 1;

        if (pageSize < 1)
            pageSize = 12;

        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        try
        {
            using var command = new SqlCommand("BrowseCourses", (SqlConnection)connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("@SearchTerm", SqlDbType.NVarChar, 200).Value = searchTerm ?? (object)DBNull.Value;
            command.Parameters.Add("@Category", SqlDbType.NVarChar, 100).Value = category ?? (object)DBNull.Value;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

            var allItems = new List<CourseItemDto>();
            var totalCount = 0;

            using var reader = await command.ExecuteReaderAsync();

            // Result Set 1: Course items
            while (reader.Read())
            {
                allItems.Add(new CourseItemDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)
                ));
            }

            // Move to Result Set 2: Total count
            await reader.NextResultAsync();
            if (reader.Read())
            {
                totalCount = reader.GetInt32(0);
            }

            // Apply org visibility filter in C# (avoids TVP complexity)
            var filteredItems = allItems;
            if (visibleCourseIds != null && visibleCourseIds.Count > 0)
            {
                filteredItems = allItems.Where(c => visibleCourseIds.Contains(c.Id)).ToList();
            }

            return new BrowseResult(filteredItems, totalCount, pageNumber, pageSize);
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }
}

/// <summary>DTO for a course item returned from BrowseAsync.</summary>
public record CourseItemDto(
    Guid Id,
    string Title,
    string ShortDescription,
    string Category,
    string Duration);

/// <summary>Paginated browse result from the stored procedure.</summary>
public record BrowseResult(
    IEnumerable<CourseItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
