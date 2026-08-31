using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Catalog;
using LibreLms.Contracts.Enrollment;
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
/// Spec 027 (R9): cross-module facts come from contracts (IUserLookup, IEnrollmentAdmin,
/// ICourseLookup) — only Management-owned data (organizations) comes from ManagementDbContext.
/// Counts keep the pre-existing semantics exactly (all Student rows, org subtree = sum of
/// per-org counts).
/// </summary>
public class DashboardService(
    ManagementDbContext managementCtx,
    IUserLookup userLookup,
    IEnrollmentAdmin enrollmentAdmin,
    ICourseLookup courseLookup)
{
    /// <summary>Get system-wide metrics (SuperUser only).</summary>
    public async Task<SystemMetricsDto> GetSystemMetricsAsync()
    {
        var totalOrgs = await managementCtx.Organizations.CountAsync(o => !o.IsDeleted);
        var totalLearners = await userLookup.CountLearnersAsync();
        var totalCourses = await courseLookup.CountAsync();
        var totalEnrollments = await enrollmentAdmin.CountEnrollmentsAsync();

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

        // Subtree = sum of the per-org counts (dev scale: a handful of orgs).
        var learnerCounts = await userLookup.GetLearnerCountsByOrgAsync();
        var learnerCount = learnerCounts
            .Where(c => descendantIds.Contains(c.OrganizationId))
            .Sum(c => c.Count);

        // Subtree counts in two bulk queries (spec 048 E7) — sum of the per-org counts.
        var courseCounts = await courseLookup.GetCourseCountsByOrgsAsync(descendantIds);
        var courseCount = courseCounts.Values.Sum();
        var enrollmentCount = await enrollmentAdmin.CountEnrollmentsByOrgsAsync(descendantIds);

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
        var learnerName = await userLookup.GetUserNameAsync(studentId);
        if (learnerName is null)
            throw new KeyNotFoundException("Student not found.");

        var enrollments = await enrollmentAdmin.GetStudentEnrollmentsAsync(studentId);
        var enrolledCount = enrollments.Count;

        // Completed courses would require checking Scorm attempts
        var completedCount = 0;
        var avgScore = 0.0;

        return new PersonalMetricsDto(enrolledCount, completedCount, avgScore, learnerName);
    }

    /// <summary>Get recent activity entries.</summary>
    public async Task<IList<RecentActivityDto>> GetRecentActivityAsync(int limit = 10)
    {
        var activities = new List<RecentActivityDto>();

        // Recent enrollments via the Enrollment module's contract (course titles resolved there)
        var recentEnrollments = await enrollmentAdmin.GetRecentEnrollmentsAsync(limit);
        foreach (var e in recentEnrollments)
        {
            activities.Add(new RecentActivityDto(
                $"{e.StudentName} enrolled in {e.CourseTitle}",
                e.EnrolledAt,
                "enrollment"
            ));
        }

        // Recent organizations (Management-owned)
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
