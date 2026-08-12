using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Endpoints;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;

namespace LibreLms.Host.Pages.Admin.Courses;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class EditCourseModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly ScormPackageService _scormService;

    [BindProperty] public Guid CourseId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ShortDescription { get; set; } = string.Empty;
    [BindProperty] public string FullDescription { get; set; } = string.Empty;
    [BindProperty] public string Category { get; set; } = string.Empty;
    [BindProperty] public string Duration { get; set; } = string.Empty;

    [BindProperty] public IFormFile? ScormFile { get; set; }

    public ScormPackage? CurrentScormPackage { get; set; }
    public string? Error { get; set; }

    public EditCourseModel(CourseCatalogService catalogService, ScormPackageService scormService)
    {
        _catalogService = catalogService;
        _scormService = scormService;
    }

    public async Task<IActionResult> OnGetAsync(Guid courseId)
    {
        CourseId = courseId;

        var course = await _catalogService.GetByIdAsync(courseId);
        if (course is null)
        {
            return RedirectToPage("./Index", new { error = "Course not found." });
        }

        Title = course.Title;
        ShortDescription = course.ShortDescription;
        FullDescription = course.FullDescription;
        Category = course.Category;
        Duration = course.Duration;

        CurrentScormPackage = await _scormService.GetPackageByCourseIdAsync(courseId);

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

            // Handle SCORM file upload
            if (ScormFile is not null && ScormFile.Length > 0)
            {
                if (ScormFile.Length > 50 * 1024 * 1024)
                {
                    Error = "SCORM file must be under 50MB.";
                    await OnGetAsync(courseId);
                    return Page();
                }

                using var stream = ScormFile.OpenReadStream();
                if (CurrentScormPackage != null)
                {
                    // Replace existing SCORM
                    var (package, replaceError) = await _scormService.ReplacePackageAsync(courseId, stream);
                    if (replaceError is not null)
                    {
                        Error = $"SCORM replacement failed: {replaceError}";
                        await OnGetAsync(courseId);
                        return Page();
                    }
                }
                else
                {
                    // Add new SCORM
                    var (package, uploadError) = await _scormService.UploadAsync(stream, courseId);
                    if (uploadError is not null)
                    {
                        Error = $"SCORM upload failed: {uploadError}";
                        await OnGetAsync(courseId);
                        return Page();
                    }
                }
            }

            return RedirectToPage("./Index", new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return RedirectToPage("./Index", new { error = "Course not found." });
        }
        catch (Exception ex)
        {
            Error = $"Failed to update course: {ex.Message}";
            await OnGetAsync(courseId);
            return Page();
        }
    }
}
