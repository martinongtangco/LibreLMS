using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Host.Pages.Admin;
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

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = AdminPageState.DefaultPageSize;

    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public AdminPaginationModel? Pagination { get; set; }

    public async Task OnGetAsync(string? student, string? course)
    {
        await LoadAsync(student, course, PageNumber, PageSize, stepBackWhenEmpty: false);
    }

    public async Task<IActionResult> OnPostCancelAsync(
        Guid enrollmentId, int pageNumber, int pageSize, string? student, string? course)
    {
        try
        {
            await _service.CancelEnrollmentAsync(enrollmentId);
            SuccessMessage = "Enrollment cancelled.";
            await LoadAsync(student, course, pageNumber, pageSize, stepBackWhenEmpty: true);
            return Page();
        }
        catch (KeyNotFoundException)
        {
            Error = "Enrollment not found.";
            await LoadAsync(student, course, pageNumber, pageSize, stepBackWhenEmpty: true);
            return Page();
        }
    }

    /// <summary>
    /// Shared load path for GET and post-cancel re-query (spec 032): normalizes the page
    /// size, trims/nulls the filters, clamps the requested page, and re-fetches when the
    /// clamp moved the page. After a row action (stepBackWhenEmpty), an empty current page
    /// past page 1 steps back to the previous page.
    /// </summary>
    private async Task LoadAsync(
        string? student, string? course, int requestedPage, int rawPageSize, bool stepBackWhenEmpty)
    {
        try
        {
            var pageSize = AdminPageState.NormalizePageSize(rawPageSize);
            var studentFilter = string.IsNullOrWhiteSpace(student) ? null : student.Trim();
            var courseFilter = string.IsNullOrWhiteSpace(course) ? null : course.Trim();
            var page = Math.Max(1, requestedPage);

            var result = await _service.ListAllEnrollmentsPagedAsync(studentFilter, courseFilter, page, pageSize);

            // Clamp a tampered/out-of-range page before render; re-fetch when the clamp moved it.
            var effective = AdminPageState.ClampPage(page, result.TotalCount, pageSize);
            if (effective != page)
            {
                result = await _service.ListAllEnrollmentsPagedAsync(studentFilter, courseFilter, effective, pageSize);
                page = effective;
            }

            // After a row action, if the current page came back empty and we are past page 1,
            // show the previous page (spec 032, interaction rule 6).
            if (stepBackWhenEmpty && result.Items.Count == 0 && page > 1)
            {
                var previous = AdminPageState.ClampPage(page - 1, result.TotalCount, pageSize);
                result = await _service.ListAllEnrollmentsPagedAsync(studentFilter, courseFilter, previous, pageSize);
                page = previous;
            }

            SearchStudent = studentFilter;
            SearchCourse = courseFilter;
            PageSize = pageSize;
            PageNumber = page;
            TotalCount = result.TotalCount;
            TotalPages = AdminPageState.TotalPages(result.TotalCount, pageSize);
            Enrollments = result.Items.Select(e => new EnrollmentDisplay(
                e.EnrollmentId,
                e.StudentName,
                e.StudentEmail,
                e.CourseTitle,
                e.OrganizationName,
                e.EnrolledAt
            )).ToList();
            Pagination = new AdminPaginationModel(
                page,
                TotalPages,
                pageSize,
                TotalCount,
                ActionUrl: "/Admin/Enrollments/Index",
                FilterQueryParams: new List<KeyValuePair<string, string?>>
                {
                    new("student", SearchStudent),
                    new("course", SearchCourse)
                },
                BuildPageUrl: p => "/Admin/Enrollments/Index?student="
                    + Uri.EscapeDataString(SearchStudent ?? "")
                    + "&course="
                    + Uri.EscapeDataString(SearchCourse ?? "")
                    + "&pageSize=" + PageSize
                    + "&pageNumber=" + p);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load enrollments: {ex.Message}";
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
