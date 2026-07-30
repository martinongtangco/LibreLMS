using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Dashboard;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly OrganizationService _orgService;

    public IndexModel(DashboardService dashboardService, OrganizationService orgService)
    {
        _dashboardService = dashboardService;
        _orgService = orgService;
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

    public async Task OnGetAsync(ClaimsPrincipal user)
    {
        try
        {
            Role = AuthHelpers.GetCurrentUserRole(user);

            if (AuthHelpers.IsSuperUser(user))
            {
                var metrics = await _dashboardService.GetSystemMetricsAsync();
                TotalOrganizations = metrics.TotalOrganizations;
                TotalLearners = metrics.TotalLearners;
                TotalCourses = metrics.TotalCourses;
                TotalEnrollments = metrics.TotalEnrollments;
                CompletionRate = metrics.AverageCompletionRate.ToString("P1");
            }
            else
            {
                var orgId = AuthHelpers.GetCurrentUserOrgId(user);
                if (orgId.HasValue)
                {
                    var metrics = await _dashboardService.GetOrgMetricsAsync(orgId.Value);
                    TotalOrganizations = metrics.OrganizationCount;
                    TotalLearners = metrics.LearnerCount;
                    TotalCourses = metrics.CourseCount;
                    TotalEnrollments = metrics.EnrollmentCount;
                    CompletionRate = metrics.AverageCompletionRate.ToString("P1");
                    OrganizationName = metrics.OrganizationName;
                }
            }

            RecentActivity = (await _dashboardService.GetRecentActivityAsync(10)).Cast<RecentActivityDto>().ToList();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load dashboard: {ex.Message}";
        }
    }
}
