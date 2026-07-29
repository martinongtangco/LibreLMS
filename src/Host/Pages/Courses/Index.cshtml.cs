using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LearningLms.Contracts.Enrollment;
using LearningLms.Modules.Catalog.Application;

namespace LearningLms.Host.Pages.Courses;

public class CourseIndexModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly IEnrollmentLookup _enrollmentLookup;

    public CourseIndexModel(
        CourseCatalogService catalogService,
        IEnrollmentLookup enrollmentLookup)
    {
        _catalogService = catalogService;
        _enrollmentLookup = enrollmentLookup;
    }

    public List<CourseItem> Courses { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }

    public async Task OnGetAsync()
    {
        var courses = await _catalogService.ListAsync(Search, Category);
        var enrolledIds = await GetEnrolledCourseIds();

        Courses = courses.Select(c => new CourseItem(
            c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
            enrolledIds.Contains(c.Id))).ToList();
        Categories = Courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>HTMX handler: return course list partial for inline swap.</summary>
    public async Task<PartialViewResult> OnGetCourseListAsync(string? search, string? category)
    {
        var courses = await _catalogService.ListAsync(search, category);
        var enrolledIds = await GetEnrolledCourseIds();

        var model = courses.Select(c => new CourseItem(
            c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
            enrolledIds.Contains(c.Id))).ToList();

        return Partial("_CourseList", model);
    }

    /// <summary>Fetch the set of course IDs the current student is enrolled in.</summary>
    private async Task<HashSet<Guid>> GetEnrolledCourseIds()
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);
        var ids = new HashSet<Guid>();

        var courses = await _catalogService.ListAsync();
        foreach (var course in courses)
        {
            if (await _enrollmentLookup.IsEnrolledAsync(studentId, course.Id))
            {
                ids.Add(course.Id);
            }
        }

        return ids;
    }
}

public record CourseListResponse(IEnumerable<CourseItem> Courses);
public record CourseItem(Guid Id, string Title, string ShortDescription, string Category, string Duration, bool IsEnrolled = false);
