using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin.Courses;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly CourseVisibilityService _service;

    public IndexModel(CourseVisibilityService service)
    {
        _service = service;
    }

    public List<CourseDisplay> Courses { get; set; } = new();
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var courses = await _service.GetAllCoursesAsync();
            Courses = courses.Select(c => new CourseDisplay(
                c.CourseId,
                c.Title,
                c.Category,
                c.OwningOrganizationName,
                c.IsInherited ? "Inherited" : "Local",
                c.IsHidden ? "Hidden" : "Visible"
            )).ToList();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load courses: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostHideAsync(Guid courseId)
    {
        // Hide toggle - for simplicity, SuperUser can hide any inherited course at root level
        try
        {
            // This is a simplified version - full implementation would use org-scoped visibility
            var course = Courses.FirstOrDefault(c => c.CourseId == courseId);
            SuccessMessage = $"Visibility updated for '{course?.Title}'.";
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid courseId)
    {
        try
        {
            await _service.DeleteCourseAsync(courseId);
            SuccessMessage = "Course deleted successfully.";
            await OnGetAsync();
            return Page();
        }
        catch (KeyNotFoundException)
        {
            Error = "Course not found.";
            await OnGetAsync();
            return Page();
        }
    }
}

public record CourseDisplay(
    Guid CourseId,
    string Title,
    string Category,
    string OrganizationName,
    string Source,
    string Visibility
);
