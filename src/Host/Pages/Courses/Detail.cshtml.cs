using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LearningLms.Contracts.Enrollment;
using LearningLms.Modules.Catalog.Application;
using LearningLms.Modules.Enrollment.Application;
using LearningLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.Courses;

public class CourseDetailModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly ScormPackageService _scormPackageService;
    private readonly EnrollmentService _enrollmentService;
    private readonly IEnrollmentLookup _enrollmentLookup;

    public CourseDetailModel(
        CourseCatalogService catalogService,
        ScormPackageService scormPackageService,
        EnrollmentService enrollmentService,
        IEnrollmentLookup enrollmentLookup)
    {
        _catalogService = catalogService;
        _scormPackageService = scormPackageService;
        _enrollmentService = enrollmentService;
        _enrollmentLookup = enrollmentLookup;
    }

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    public CourseDetailItem? Course { get; set; }
    public bool IsEnrolled { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var course = await _catalogService.GetByIdAsync(Id);
            if (course is null)
                return;

            var studentId = ScormHelpers.GetStudentId(HttpContext);
            var enrolled = await _enrollmentLookup.IsEnrolledAsync(studentId, Id);
            var scormPkg = await _scormPackageService.GetPackageByCourseIdAsync(Id);

            Course = new CourseDetailItem(
                Id: course.Id,
                Title: course.Title,
                ShortDescription: course.ShortDescription,
                FullDescription: course.FullDescription,
                Category: course.Category,
                Duration: course.Duration,
                IsEnrolled: enrolled,
                IsScorm: scormPkg is not null,
                ScormPackageId: scormPkg?.Id);

            IsEnrolled = enrolled;
        }
        catch
        {
            var scormPkg = await _scormPackageService.GetPackageByCourseIdAsync(courseId);
            return new EnrollmentResult(
                false,
                "You are already enrolled in this course.",
                "warning",
                courseId,
                scormPkg is not null);
        }

        var scormPkg2 = await _scormPackageService.GetPackageByCourseIdAsync(courseId);
        return new EnrollmentResult(
            true,
            "Successfully enrolled!",
            "success",
            courseId,
            scormPkg2 is not null);
    }

    /// <summary>HTMX handler: return course detail partial for inline swap (US3).</summary>
    public async Task<PartialViewResult> OnGetDetailAsync(Guid id)
    {
        try
        {
            var course = await _catalogService.GetByIdAsync(id);
            if (course is null)
            {
                return Partial("_ErrorPartial", "Course not found");
            }

            var studentId = ScormHelpers.GetStudentId(HttpContext);
            var enrolled = await _enrollmentLookup.IsEnrolledAsync(studentId, id);
            var scormPkg = await _scormPackageService.GetPackageByCourseIdAsync(id);

            var model = new CourseDetailItem(
                Id: course.Id,
                Title: course.Title,
                ShortDescription: course.ShortDescription,
                FullDescription: course.FullDescription,
                Category: course.Category,
                Duration: course.Duration,
                IsEnrolled: enrolled,
                IsScorm: scormPkg is not null,
                ScormPackageId: scormPkg?.Id);

            return Partial("_CourseDetail", model);
        }
        catch
        {
            return Partial("_ErrorPartial", "Unable to load data. Please refresh.");
        }
    }

    /// <summary>HTMX handler: enroll in a course and return result partial (US2).</summary>
    public async Task<PartialViewResult> OnPostEnrollAsync(Guid courseId)
    {
        try
        {
            var result = await TryEnrollAsync(courseId);
            return Partial("_EnrollmentResult", result);
        }
        catch
        {
            return Partial("_ErrorPartial", "Unable to process enrollment. Please try again.");
        }
    }

    /// <summary>Attempt to enroll the current student in a course.</summary>
    private async Task<EnrollmentResult> TryEnrollAsync(Guid courseId)
    {
        var studentId = ScormHelpers.GetStudentId(HttpContext);
        var (enrollment, isDuplicate, courseNotFound) = await _enrollmentService.EnrollAsync(studentId, courseId);

        if (courseNotFound)
            return new EnrollmentResult(false, "Course not found.", "error", courseId);

        if (isDuplicate)
        {
            var scormPkg = await _scormPackageService.GetPackageByCourseIdAsync(courseId);
            return new EnrollmentResult(
                false,
                "You are already enrolled in this course.",
                "warning",
                courseId,
                scormPkg is not null);
        }

        var scormPkg2 = await _scormPackageService.GetPackageByCourseIdAsync(courseId);
        return new EnrollmentResult(
            true,
            "Successfully enrolled!",
            "success",
            courseId,
            scormPkg2 is not null);
    }
}

public record CourseDetail(
    Guid Id, string Title, string ShortDescription, string FullDescription,
    string Category, string Duration, bool IsScorm, Guid? ScormPackageId);

/// <summary>View model for course detail partial views (HTMX swaps).</summary>
public record CourseDetailItem(
    Guid Id,
    string Title,
    string ShortDescription,
    string FullDescription,
    string Category,
    string Duration,
    bool IsEnrolled,
    bool IsScorm,
    Guid? ScormPackageId);

/// <summary>View model for enrollment result partial view (HTMX feedback).</summary>
public record EnrollmentResult(
    bool Success,
    string Message,
    string MessageType,
    Guid CourseId,
    bool? IsScorm = null);

public record MyEnrollmentsResponse(IEnumerable<MyEnrollmentItem> Enrollments);
public record MyEnrollmentItem(Guid Id, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);
