using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Management.Application;
using LibreLms.SharedKernel;

namespace LibreLms.Host.Pages.Admin.Enrollments;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class BulkEnrollModel : PageModel
{
    private readonly AdminEnrollmentService _enrollmentService;
    private readonly UserService _userService;
    private readonly CourseCatalogService _catalogService;

    public BulkEnrollModel(
        AdminEnrollmentService enrollmentService,
        UserService userService,
        CourseCatalogService catalogService)
    {
        _enrollmentService = enrollmentService;
        _userService = userService;
        _catalogService = catalogService;
    }

    public SelectList? Courses { get; set; }
    public SelectList? Students { get; set; }
    public string? SelectedCourseId { get; set; }
    public List<string> SelectedStudentIds { get; set; } = new();
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public int? EnrolledCount { get; set; }
    public int? SkippedCount { get; set; }

    public async Task OnGetAsync(string? courseId)
    {
        SelectedCourseId = courseId;

        var allCourses = await _catalogService.ListAsync();
        Courses = new SelectList(
            allCourses.Select(c => new SelectListItem(c.Title, c.Id.ToString())),
            "Value", "Text", SelectedCourseId);

        var allUsers = await _userService.ListAllAsync(RoleNames.Learner);
        Students = new SelectList(
            allUsers.Select(u => new SelectListItem($"{u.Name} ({u.Email})", u.Id.ToString())),
            "Value", "Text");
    }

    public async Task<IActionResult> OnPostAsync(
        string courseId,
        List<string> studentIds)
    {
        try
        {
            if (!Guid.TryParse(courseId, out var courseGuid))
            {
                Error = "Invalid course selected.";
                await OnGetAsync(courseId);
                return Page();
            }

            if (studentIds == null || !studentIds.Any())
            {
                Error = "Please select at least one learner.";
                await OnGetAsync(courseId);
                return Page();
            }

            var validStudentIds = studentIds
                .Where(s => Guid.TryParse(s, out _))
                .Select(Guid.Parse)
                .ToList();

            if (!validStudentIds.Any())
            {
                Error = "No valid learners selected.";
                await OnGetAsync(courseId);
                return Page();
            }

            var result = await _enrollmentService.BulkEnrollAsync(validStudentIds, courseGuid);
            SuccessMessage = $"Bulk enrollment complete: {result.Enrolled} enrolled, {result.Skipped} skipped, {result.Errors} errors.";
            EnrolledCount = result.Enrolled;
            SkippedCount = result.Skipped;

            if (result.ErrorMessages.Any())
            {
                Error = $"Errors: {string.Join("; ", result.ErrorMessages)}";
            }

            await OnGetAsync(courseId);
            return Page();
        }
        catch (KeyNotFoundException ex)
        {
            Error = ex.Message;
            await OnGetAsync(null);
            return Page();
        }
        catch (ArgumentException ex)
        {
            Error = ex.Message;
            await OnGetAsync(null);
            return Page();
        }
    }
}
