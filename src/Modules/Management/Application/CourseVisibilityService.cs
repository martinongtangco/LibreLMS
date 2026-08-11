using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Catalog.Infrastructure;
using LibreLms.Modules.Management.Domain;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>DTO for a course with visibility context.</summary>
public record CourseVisibilityDto(
    Guid CourseId,
    string Title,
    string Category,
    Guid OwningOrganizationId,
    string OwningOrganizationName,
    bool IsInherited,
    bool IsHidden
);

/// <summary>DTO for a visibility override.</summary>
public record VisibilityOverrideDto(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    Guid CourseOrgId,
    Guid OrganizationId,
    bool IsHidden,
    DateTimeOffset CreatedAt
);

/// <summary>Service for managing course visibility within organizational hierarchies.</summary>
public class CourseVisibilityService(
    ManagementDbContext managementCtx,
    CatalogDbContext catalogCtx,
    IOrganizationLookup orgLookup)
{
    /// <summary>
    /// Get all visible courses for an organization.
    /// Includes local courses and inherited courses from ancestors,
    /// minus any courses that have been explicitly hidden.
    /// </summary>
    public async Task<IList<CourseVisibilityDto>> GetVisibleCoursesAsync(Guid orgId)
    {
        // Get all ancestor org IDs including the target org
        var ancestorIds = await orgLookup.GetAncestorOrgIdsAsync(orgId);

        // Get all courses from the org and its ancestors
        var allCourses = await catalogCtx.Courses
            .Where(c => ancestorIds.Contains(c.OrganizationId))
            .ToListAsync();

        // Get the local org ID for comparison
        var localOrgId = orgId;

        // Get visibility overrides for this org
        var overrides = await managementCtx.CourseVisibilityOverrides
            .Where(o => o.OrganizationId == orgId)
            .ToListAsync();

        var hiddenCourseIds = overrides
            .Where(o => o.IsHidden)
            .Select(o => o.CourseId)
            .ToHashSet();

        // Build org name cache
        var orgNameCache = new Dictionary<Guid, string>();

        var result = new List<CourseVisibilityDto>();
        foreach (var course in allCourses)
        {
            var isInherited = course.OrganizationId != localOrgId;
            var isHidden = hiddenCourseIds.Contains(course.Id);

            if (!orgNameCache.TryGetValue(course.OrganizationId, out var orgName))
            {
                var org = await orgLookup.GetOrganizationAsync(course.OrganizationId);
                orgName = org?.Name ?? "Unknown";
                orgNameCache[course.OrganizationId] = orgName;
            }

            result.Add(new CourseVisibilityDto(
                course.Id,
                course.Title,
                course.Category,
                course.OrganizationId,
                orgName,
                isInherited,
                isHidden
            ));
        }

        return result.OrderBy(c => c.Title).ToList();
    }

    /// <summary>
    /// Set a visibility override for an inherited course in a specific organization.
    /// </summary>
    public async Task<CourseVisibilityOverride> SetVisibilityOverrideAsync(
        Guid orgId, Guid courseId, bool isHidden, Guid? createdBy)
    {
        // Verify the course exists
        var course = await catalogCtx.Courses.FindAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        // Only inherited courses can be overridden (not owned by this org)
        if (course.OrganizationId == orgId)
            throw new InvalidOperationException("Cannot override visibility of a locally-owned course.");

        // Check for existing override
        var existing = await managementCtx.CourseVisibilityOverrides
            .FirstOrDefaultAsync(o => o.OrganizationId == orgId && o.CourseId == courseId);

        if (existing is not null)
        {
            existing.IsHidden = isHidden;
            existing.CreatedBy = createdBy;
            await managementCtx.SaveChangesAsync();
            return existing;
        }

        var override_ = new CourseVisibilityOverride
        {
            OrganizationId = orgId,
            CourseId = courseId,
            IsHidden = isHidden,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        managementCtx.CourseVisibilityOverrides.Add(override_);
        await managementCtx.SaveChangesAsync();
        return override_;
    }

    /// <summary>
    /// Get all visibility overrides for an organization.
    /// </summary>
    public async Task<IList<VisibilityOverrideDto>> GetOverridesAsync(Guid orgId)
    {
        var overrides = await managementCtx.CourseVisibilityOverrides
            .Where(o => o.OrganizationId == orgId)
            .ToListAsync();

        var result = new List<VisibilityOverrideDto>();
        foreach (var o in overrides)
        {
            var course = await catalogCtx.Courses.FindAsync(o.CourseId);
            result.Add(new VisibilityOverrideDto(
                o.Id,
                o.CourseId,
                course?.Title ?? "Unknown",
                course?.OrganizationId ?? Guid.Empty,
                o.OrganizationId,
                o.IsHidden,
                o.CreatedAt
            ));
        }

        return result;
    }

    /// <summary>
    /// Get all courses in the system with organization info (for admin listing).
    /// </summary>
    public async Task<IList<CourseVisibilityDto>> GetAllCoursesAsync()
    {
        var allCourses = await catalogCtx.Courses.ToListAsync();
        var orgNameCache = new Dictionary<Guid, string>();
        var result = new List<CourseVisibilityDto>();

        foreach (var course in allCourses)
        {
            if (!orgNameCache.TryGetValue(course.OrganizationId, out var orgName))
            {
                var org = await orgLookup.GetOrganizationAsync(course.OrganizationId);
                orgName = org?.Name ?? "Unknown";
                orgNameCache[course.OrganizationId] = orgName;
            }

            result.Add(new CourseVisibilityDto(
                course.Id, course.Title, course.Category, course.OrganizationId, orgName, false, false));
        }

        return result.OrderBy(c => c.Title).ToList();
    }

    /// <summary>
    /// Delete a course by ID.
    /// </summary>
    public async Task DeleteCourseAsync(Guid courseId)
    {
        var course = await catalogCtx.Courses.FindAsync(courseId);
        if (course is null)
            throw new KeyNotFoundException("Course not found.");

        // Remove any visibility overrides for this course
        var overrides = await managementCtx.CourseVisibilityOverrides
            .Where(o => o.CourseId == courseId)
            .ToListAsync();
        managementCtx.CourseVisibilityOverrides.RemoveRange(overrides);

        catalogCtx.Courses.Remove(course);
        await managementCtx.SaveChangesAsync();
        await catalogCtx.SaveChangesAsync();
    }
}
