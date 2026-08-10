using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Management.Application;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Dashboard;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly OrganizationService _orgService;
    private readonly EnrollmentService _enrollmentService;
    private readonly CourseVisibilityService _visibilityService;

    public IndexModel(
        DashboardService dashboardService,
        OrganizationService orgService,
        EnrollmentService enrollmentService,
        CourseVisibilityService visibilityService)
    {
        _dashboardService = dashboardService;
        _orgService = orgService;
        _enrollmentService = enrollmentService;
        _visibilityService = visibilityService;
    }

    public string Role { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public int TotalOrganizations { get; set; }
    public int TotalLearners { get; set; }
    public int TotalCourses { get; set; }
    public int TotalEnrollments { get; set; }
    public string? CompletionRate { get; set; }
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
    public string? Error { get; set; }
    public List<CourseRow> AllCourses { get; set; } = new();

    public record CourseRow(string Title, string Category, int EnrollmentCount);

    public async Task OnGetAsync(ClaimsPrincipal user)
    {
        try
        {
            Role = AuthHelpers.GetCurrentUserRole(user);
            var isSuperUser = AuthHelpers.IsSuperUser(user);
            var orgId = AuthHelpers.GetCurrentUserOrgId(user);

            if (isSuperUser)
            {
                var metrics = await _dashboardService.GetSystemMetricsAsync();
                TotalOrganizations = metrics.TotalOrganizations;
                TotalLearners = metrics.TotalLearners;
                TotalCourses = metrics.TotalCourses;
                TotalEnrollments = metrics.TotalEnrollments;
                CompletionRate = metrics.AverageCompletionRate.ToString("P1");
            }
            else if (orgId.HasValue)
            {
                var metrics = await _dashboardService.GetOrgMetricsAsync(orgId.Value);
                TotalOrganizations = metrics.OrganizationCount;
                TotalLearners = metrics.LearnerCount;
                TotalCourses = metrics.CourseCount;
                TotalEnrollments = metrics.EnrollmentCount;
                CompletionRate = metrics.AverageCompletionRate.ToString("P1");
                OrganizationName = metrics.OrganizationName;
            }

            RecentActivity = (await _dashboardService.GetRecentActivityAsync(10)).Cast<RecentActivityDto>().ToList();

            // Load all visible courses with enrollment counts
            if (isSuperUser)
            {
                var visibleCourses = await _visibilityService.GetAllCoursesAsync();
                var courseIds = visibleCourses.Select(c => c.CourseId).ToList();
                var enrollmentCounts = await _enrollmentService.GetEnrollmentCountsByCourseAsync(courseIds);

                AllCourses = visibleCourses.Select(c => new CourseRow(
                    c.Title,
                    c.Category,
                    enrollmentCounts.TryGetValue(c.CourseId, out var count) ? count : 0
                )).ToList();
            }
            else if (orgId.HasValue)
            {
                var visibleCourses = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
                var courseIds = visibleCourses.Select(c => c.CourseId).ToList();
                var enrollmentCounts = await _enrollmentService.GetEnrollmentCountsByCourseAsync(courseIds);

                AllCourses = visibleCourses.Select(c => new CourseRow(
                    c.Title,
                    c.Category,
                    enrollmentCounts.TryGetValue(c.CourseId, out var count) ? count : 0
                )).ToList();
            }
        }
        catch (Exception ex)
        {
            Error = $"Failed to load dashboard: {ex.Message}";
        }
    }
}
