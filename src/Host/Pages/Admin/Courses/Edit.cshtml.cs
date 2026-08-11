using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Endpoints;

namespace LibreLms.Host.Pages.Admin.Courses;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class EditCourseModel : PageModel
{
    private readonly CourseCatalogService _catalogService;

    [BindProperty] public Guid CourseId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ShortDescription { get; set; } = string.Empty;
    [BindProperty] public string FullDescription { get; set; } = string.Empty;
    [BindProperty] public string Category { get; set; } = string.Empty;
    [BindProperty] public string Duration { get; set; } = string.Empty;

    public string? Error { get; set; }

    public EditCourseModel(CourseCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public async Task<IActionResult> OnGetAsync(Guid courseId)
    {
        CourseId = courseId;

        var course = await _catalogService.GetByIdAsync(courseId);
        if (course is null)
        {
            return RedirectToPage("/Admin/Courses", new { error = "Course not found." });
        }

        Title = course.Title;
        ShortDescription = course.ShortDescription;
        FullDescription = course.FullDescription;
        Category = course.Category;
        Duration = course.Duration;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid courseId)
    {
        CourseId = courseId;

        try
        {
            await _catalogService.UpdateAsync(courseId,
                new UpdateCourseRequest(
                    Title,
                    ShortDescription,
                    FullDescription,
                    Category,
                    Duration
                ));

            return RedirectToPage("/Admin/Courses", new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return RedirectToPage("/Admin/Courses", new { error = "Course not found." });
        }
        catch (Exception ex)
        {
            Error = $"Failed to update course: {ex.Message}";
            return Page();
        }
    }
}
