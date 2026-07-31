using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin.Enrollments;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class IndexModel : PageModel
{
    private readonly AdminEnrollmentService _service;

    public IndexModel(AdminEnrollmentService service)
    {
        _service = service;
    }

    public List<EnrollmentDisplay> Enrollments { get; set; } = new();
    public string? SearchStudent { get; set; }
    public string? SearchCourse { get; set; }
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string? student, string? course)
    {
        try
        {
            SearchStudent = student;
            SearchCourse = course;

            var enrollments = await _service.ListAllEnrollmentsAsync(student, course);
            Enrollments = enrollments.Select(e => new EnrollmentDisplay(
                e.EnrollmentId,
                e.StudentName,
                e.StudentEmail,
                e.CourseTitle,
                e.OrganizationName,
                e.EnrolledAt
            )).ToList();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load enrollments: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid enrollmentId)
    {
        try
        {
            await _service.CancelEnrollmentAsync(enrollmentId);
            SuccessMessage = "Enrollment cancelled.";
            await OnGetAsync(SearchStudent, SearchCourse);
            return Page();
        }
        catch (KeyNotFoundException)
        {
            Error = "Enrollment not found.";
            await OnGetAsync(SearchStudent, SearchCourse);
            return Page();
        }
    }
}

public record EnrollmentDisplay(
    Guid EnrollmentId,
    string StudentName,
    string StudentEmail,
    string CourseTitle,
    string OrganizationName,
    DateTimeOffset EnrolledAt
);
