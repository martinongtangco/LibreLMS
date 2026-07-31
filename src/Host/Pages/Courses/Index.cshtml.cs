using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Contracts.Enrollment;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Courses;

public class CourseIndexModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly IEnrollmentLookup _enrollmentLookup;
    private readonly CourseVisibilityService _visibilityService;

    public CourseIndexModel(
        CourseCatalogService catalogService,
        IEnrollmentLookup enrollmentLookup,
        CourseVisibilityService visibilityService)
    {
        _catalogService = catalogService;
        _enrollmentLookup = enrollmentLookup;
        _visibilityService = visibilityService;
    }

    public List<CourseItem> Courses { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }

    public async Task OnGetAsync()
    {
        var (courses, enrolledIds) = await GetCoursesAndEnrollments();

        Courses = courses.Select(c => new CourseItem(
            c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
            enrolledIds.Contains(c.Id))).ToList();
        Categories = Courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>HTMX handler: return course list partial for inline swap.</summary>
    public async Task<PartialViewResult> OnGetCourseListAsync(string? search, string? category)
    {
        var (courses, enrolledIds) = await GetCoursesAndEnrollments(search, category);

        var model = courses.Select(c => new CourseItem(
            c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
            enrolledIds.Contains(c.Id))).ToList();

        return Partial("_CourseList", model);
    }

    /// <summary>Get org-scoped courses and enrolled course IDs.</summary>
    private async Task<(IEnumerable<LibreLms.Modules.Catalog.Domain.Course>, HashSet<Guid>)> GetCoursesAndEnrollments(string? search = null, string? category = null)
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);
        var enrolledIds = new HashSet<Guid>();
        IEnumerable<LibreLms.Modules.Catalog.Domain.Course> courses;

        // Check if user is authenticated with org context
        var role = HttpContext.User.Identity?.IsAuthenticated == true
            ? HttpContext.User.FindFirstValue(ClaimTypes.Role)
            : null;
        var orgId = role is not null
            ? AuthHelpers.GetCurrentUserOrgId(HttpContext.User)
            : null;

        if (orgId.HasValue)
        {
            // Authenticated user with org — show org-visible courses
            var visible = await _visibilityService.GetVisibleCoursesAsync(orgId.Value);
            // Get full course details for visible courses
            var visibleCourseIds = visible.ToDictionary(v => v.CourseId);
            var allCourses = await _catalogService.ListAsync(search, category);
            courses = allCourses.Where(c => visibleCourseIds.ContainsKey(c.Id));
        }
        else
        {
            // Unauthenticated or no org — show all courses
            courses = await _catalogService.ListAsync(search, category);
        }

        // Check enrollment status
        foreach (var course in courses)
        {
            if (await _enrollmentLookup.IsEnrolledAsync(studentId, course.Id))
            {
                enrolledIds.Add(course.Id);
            }
        }

        return (courses, enrolledIds);
    }
}

public record CourseListResponse(IEnumerable<CourseItem> Courses);
public record CourseItem(Guid Id, string Title, string ShortDescription, string Category, string Duration, bool IsEnrolled = false);
