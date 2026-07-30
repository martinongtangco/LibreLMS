using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.MyCourses;

public class MyCoursesModel : PageModel
{
    private readonly EnrollmentService _enrollmentService;
    private readonly ScormAttemptService _scormAttemptService;

    public MyCoursesModel(
        EnrollmentService enrollmentService,
        ScormAttemptService scormAttemptService)
    {
        _enrollmentService = enrollmentService;
        _scormAttemptService = scormAttemptService;
    }

    public List<EnrollmentRow> EnrollmentRows { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadEnrollments();
    }

    /// <summary>HTMX handler: return enrollment list partial for inline refresh (US4).</summary>
    [Authorize]
    public async Task<PartialViewResult> OnGetEnrollmentsAsync()
    {
        var model = await BuildEnrollmentRows();
        return Partial("_EnrollmentList", model);
    }

    private async Task LoadEnrollments()
    {
        var rows = await BuildEnrollmentRows();
        EnrollmentRows = rows;
    }

    private async Task<List<EnrollmentRow>> BuildEnrollmentRows()
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);

        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(studentId);
        var attempts = await _scormAttemptService.GetMyAttemptsAsync(studentId);

        // Join enrollments with latest SCORM attempt per course
        var attemptByCourse = attempts
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptNumber).First());

        return enrollments.Select(e =>
        {
            var attempt = attemptByCourse.TryGetValue(e.Enrollment.CourseId, out var a) ? a : null;
            return new EnrollmentRow(
                EnrollmentId: e.Enrollment.Id,
                CourseId: e.Enrollment.CourseId,
                CourseTitle: e.CourseTitle,
                EnrolledAt: e.Enrollment.EnrolledAt,
                LatestStatus: attempt?.Status,
                LatestScore: attempt?.ScoreRaw);
        }).ToList();
    }
}

/// <summary>View model for enrollment row partial view (HTMX swaps).</summary>
public record EnrollmentRow(
    Guid EnrollmentId,
    Guid CourseId,
    string CourseTitle,
    DateTimeOffset EnrolledAt,
    string? LatestStatus,
    double? LatestScore);
