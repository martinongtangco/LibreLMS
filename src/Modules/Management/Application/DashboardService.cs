using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Catalog.Infrastructure;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.Modules.Management.Infrastructure;

namespace LibreLms.Modules.Management.Application;

/// <summary>DTO for system-wide dashboard metrics.</summary>
public record SystemMetricsDto(
    int TotalOrganizations,
    int TotalLearners,
    int TotalCourses,
    int TotalEnrollments,
    double AverageCompletionRate
);

/// <summary>DTO for org-scoped dashboard metrics.</summary>
public record OrgMetricsDto(
    int OrganizationCount,
    int LearnerCount,
    int CourseCount,
    int EnrollmentCount,
    double AverageCompletionRate,
    string OrganizationName
);

/// <summary>DTO for personal learner metrics.</summary>
public record PersonalMetricsDto(
    int EnrolledCourseCount,
    int CompletedCourseCount,
    double AverageScore,
    string LearnerName
);

/// <summary>DTO for a recent activity entry.</summary>
public record RecentActivityDto(
    string Description,
    DateTimeOffset OccurredAt,
    string ActivityType
);

/// <summary>
/// Service for aggregating dashboard metrics at different organizational scopes.
/// Uses raw SQL queries for performance (SC-004: 3-second render requirement).
/// </summary>
public class DashboardService(
    ManagementDbContext managementCtx,
    EnrollmentDbContext enrollmentCtx,
    CatalogDbContext catalogCtx)
{
    /// <summary>Get system-wide metrics (SuperUser only).</summary>
    public async Task<SystemMetricsDto> GetSystemMetricsAsync()
    {
        var totalOrgs = await managementCtx.Organizations.CountAsync(o => !o.IsDeleted);
        var totalLearners = await enrollmentCtx.Students.CountAsync();
        var totalCourses = await catalogCtx.Courses.CountAsync();
        var totalEnrollments = await enrollmentCtx.Enrollments.CountAsync();

        // Completion rate is tracked in Scorm module via CourseAttempts
        // For now, use a placeholder calculation
        var avgCompletionRate = 0.0;

        return new SystemMetricsDto(totalOrgs, totalLearners, totalCourses, totalEnrollments, avgCompletionRate);
    }

    /// <summary>Get metrics scoped to an organization and its descendants.</summary>
    public async Task<OrgMetricsDto> GetOrgMetricsAsync(Guid orgId)
    {
        // Get all descendant org IDs
        var descendantIds = await GetDescendantOrgIdsAsync(orgId);
        descendantIds.Add(orgId); // Include the org itself

        var orgName = await managementCtx.Organizations
            .Where(o => o.Id == orgId && !o.IsDeleted)
            .Select(o => o.Name)
            .FirstOrDefaultAsync();

        var learnerCount = await enrollmentCtx.Students
            .CountAsync(s => descendantIds.Contains(s.OrganizationId));

        var courseCount = await catalogCtx.Courses
            .CountAsync(c => descendantIds.Contains(c.OrganizationId));

        var enrollmentCount = await enrollmentCtx.Enrollments
            .Join(
                enrollmentCtx.Students,
                e => e.StudentId,
                s => s.Id,
                (e, s) => new { Enrollment = e, Student = s })
            .CountAsync(x => descendantIds.Contains(x.Student.OrganizationId));

        return new OrgMetricsDto(
            descendantIds.Count - 1, // Exclude the org itself from the count
            learnerCount,
            courseCount,
            enrollmentCount,
            0.0, // Placeholder for completion rate
            orgName ?? "Unknown");
    }

    /// <summary>Get personal metrics for a learner.</summary>
    public async Task<PersonalMetricsDto> GetPersonalMetricsAsync(Guid studentId)
    {
        var student = await enrollmentCtx.Students.FindAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("Student not found.");

        var enrolledCount = await enrollmentCtx.Enrollments
            .CountAsync(e => e.StudentId == studentId);

        // Completed courses would require checking Scorm attempts
        var completedCount = 0;
        var avgScore = 0.0;

        return new PersonalMetricsDto(enrolledCount, completedCount, avgScore, student.Name);
    }

    /// <summary>Get recent activity entries.</summary>
    public async Task<IList<RecentActivityDto>> GetRecentActivityAsync(int limit = 10)
    {
        var activities = new List<RecentActivityDto>();

        // Recent enrollments: query enrollments+students from EnrollmentDbContext,
        // then look up course titles from CatalogDbContext in memory (cross-context joins
        // are not supported by EF Core).
        var recentEnrollmentData = await enrollmentCtx.Enrollments
            .Join(
                enrollmentCtx.Students,
                e => e.StudentId,
                s => s.Id,
                (e, s) => new { Enrollment = e, Student = s })
            .OrderByDescending(x => x.Enrollment.EnrolledAt)
            .Take(limit)
            .ToListAsync();

        // Load course titles for the enrolled course IDs
        var courseIds = recentEnrollmentData.Select(x => x.Enrollment.CourseId).ToList();
        var courseTitles = new Dictionary<Guid, string>();
        if (courseIds.Count > 0)
        {
            var courses = await catalogCtx.Courses
                .Where(c => courseIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Title);

            courseTitles = courses;
        }

        foreach (var e in recentEnrollmentData)
        {
            var courseTitle = courseTitles.TryGetValue(e.Enrollment.CourseId, out var title)
                ? title
                : $"Course ({e.Enrollment.CourseId})";

            activities.Add(new RecentActivityDto(
                $"{e.Student.Name} enrolled in {courseTitle}",
                e.Enrollment.EnrolledAt,
                "enrollment"
            ));
        }

        // Recent organizations
        var recentOrgs = await managementCtx.Organizations
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();

        foreach (var o in recentOrgs)
        {
            activities.Add(new RecentActivityDto(
                $"Organization '{o.Name}' created",
                o.CreatedAt,
                "organization"
            ));
        }

        // Sort all activities and take top N
        return activities
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToList();
    }

    private async Task<HashSet<Guid>> GetDescendantOrgIdsAsync(Guid orgId)
    {
        var ids = new HashSet<Guid>();
        var queue = new Queue<Guid>(new[] { orgId });

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = await managementCtx.Organizations
                .Where(o => o.ParentId == currentId && !o.IsDeleted)
                .Select(o => o.Id)
                .ToListAsync();

            foreach (var childId in children)
            {
                ids.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return ids;
    }
}
