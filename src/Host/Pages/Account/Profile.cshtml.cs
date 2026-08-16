using System.Security.Claims;
using LibreLms.Contracts.Enrollment;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Scorm.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Self-service profile (spec 030): an editable display name gated on the account's
/// email-verified state (FR-001..FR-004, R1/R8), a "My Courses" area grouping the
/// user's enrollments into Completed vs Enrolled (FR-005..FR-007, R6), and a
/// display-photo upload (FR-008..FR-011, R3/R4). After any successful change the
/// auth cookie is re-issued from the fresh Student row so the upper-right nav shows
/// the update without a re-login (R2 "RefreshSignIn").
/// </summary>
[Authorize]
public class ProfileModel : PageModel
{
    /// <summary>FR-003: max display-name length.</summary>
    private const int MaxNameLength = 100;

    /// <summary>FR-010: allowed display-photo extensions (case-insensitive).</summary>
    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    /// <summary>FR-010: allowed display-photo MIME types (case-insensitive).</summary>
    private static readonly HashSet<string> AllowedPhotoMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    /// <summary>FR-010: max display-photo size (5 MB).</summary>
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    /// <summary>TempData key for the save-success message carried across the PRG redirect.</summary>
    private const string SuccessTempKey = "ProfileSuccess";

    private readonly IUserProvisioning _provisioning;
    private readonly RegistrationService _registrationService;
    private readonly EnrollmentService _enrollmentService;
    private readonly ScormAttemptService _scormAttemptService;
    private readonly AuthCookieRefresher _cookieRefresher;
    private readonly IWebHostEnvironment _environment;

    public ProfileModel(
        IUserProvisioning provisioning,
        RegistrationService registrationService,
        EnrollmentService enrollmentService,
        ScormAttemptService scormAttemptService,
        AuthCookieRefresher cookieRefresher,
        IWebHostEnvironment environment)
    {
        _provisioning = provisioning;
        _registrationService = registrationService;
        _enrollmentService = enrollmentService;
        _scormAttemptService = scormAttemptService;
        _cookieRefresher = cookieRefresher;
        _environment = environment;
    }

    // ── Personal details (fresh read — the gate is checked from the DB, R1) ──

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;

    /// <summary>FR-002 gate state, read from the DB at page load and at save time.</summary>
    public bool IsEmailVerified { get; set; } = true;

    /// <summary>Display-photo URL path (e.g. "/avatars/&lt;guid&gt;.png") or null = no photo.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>First letter of the display name, uppercased — the initials placeholder.</summary>
    public string AvatarInitial => Name.Length > 0 ? char.ToUpperInvariant(Name[0]).ToString() : "?";

    // ── Messages ───────────────────────────────────────────────────────────────

    public string? SuccessMessage { get; set; }
    public string? NameError { get; set; }
    public string? PhotoError { get; set; }

    /// <summary>Set when a name save was refused because the email is unverified (FR-002).</summary>
    public bool VerificationRequired { get; set; }

    /// <summary>Neutral result of the in-profile resend (R8) + whether a link was sent.</summary>
    public string? ResendMessage { get; set; }
    public bool ResendSucceeded { get; set; }

    // ── My Courses (R6) ────────────────────────────────────────────────────────

    public List<CourseRow> CompletedCourses { get; set; } = new();
    public List<CourseRow> EnrolledCourses { get; set; } = new();

    /// <summary>FR-014: friendly error shown in the courses area only when the course
    /// data fails to load — the personal details still render.</summary>
    public string? CoursesError { get; set; }

    public sealed record CourseRow(string Title, string StatusLabel);

    // ── Handlers ───────────────────────────────────────────────────────────────

    public async Task OnGetAsync()
    {
        await LoadModelAsync(GetStudentId());

        // Post-Redirect-Get: carry the save-success message across the redirect
        // (the save handlers redirect here so the re-issued cookie is already in
        // effect when the nav re-renders, FR-004).
        if (TempData[SuccessTempKey] is string message)
        {
            SuccessMessage = message;
            TempData.Remove(SuccessTempKey);
        }
    }

    /// <summary>
    /// Name save (FR-001/FR-002/FR-003): validate (trimmed non-empty, ≤ 100 chars,
    /// no line breaks) → gate on the DB's verified state → persist via the Enrollment
    /// contract → re-issue the cookie (R2) so the nav shows the new name immediately.
    /// Rejections persist nothing.
    /// </summary>
    public async Task<IActionResult> OnPostNameAsync(string name)
    {
        var studentId = GetStudentId();
        var trimmed = name?.Trim() ?? string.Empty;

        // NOTE: LoadModelAsync resets the message properties, so it always runs
        // BEFORE the handler sets its message.
        if (trimmed.Length == 0)
        {
            await LoadModelAsync(studentId);
            NameError = "Name is required.";
            return Page();
        }
        if (trimmed.Length > MaxNameLength)
        {
            await LoadModelAsync(studentId);
            NameError = "Name must be 100 characters or fewer.";
            return Page();
        }
        if (trimmed.Contains('\r') || trimmed.Contains('\n'))
        {
            await LoadModelAsync(studentId);
            NameError = "Name must not contain line breaks.";
            return Page();
        }

        var student = await _provisioning.GetByIdAsync(studentId);
        if (student is null)
        {
            await LoadModelAsync(studentId);
            NameError = "Your account could not be found. Please sign in again.";
            return Page();
        }

        // FR-002: the gate is checked from the DB at save time, never from claims.
        if (!student.IsEmailVerified)
        {
            await LoadModelAsync(studentId);
            VerificationRequired = true;
            return Page();
        }

        try
        {
            var updated = await _provisioning.UpdateAsync(studentId, trimmed, null, null);
            await _cookieRefresher.RefreshAsync(HttpContext, updated);

            // Post-Redirect-Get: the re-issued cookie only takes effect on the NEXT
            // request, so redirect to a fresh GET — the nav then renders the new
            // name on the resulting page (FR-004). Errors re-render in place.
            TempData[SuccessTempKey] = "Profile updated.";
            return RedirectToPage();
        }
        catch (Exception)
        {
            await LoadModelAsync(studentId);
            NameError = "Sorry, your name could not be saved. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Photo save (FR-008..FR-010): validate (file present, extension + MIME in the
    /// whitelist, ≤ 5 MB) → write to wwwroot/avatars/{studentId-lower}{ext} via a temp
    /// file → move into place → update the AvatarPath column → delete the replaced
    /// file (when different) → re-issue the cookie (R2/R3). Rejections and disk/DB
    /// failures leave the previous photo untouched.
    /// </summary>
    public async Task<IActionResult> OnPostPhotoAsync(IFormFile? avatar)
    {
        var studentId = GetStudentId();

        // NOTE: LoadModelAsync resets the message properties, so it always runs
        // BEFORE the handler sets its message.
        if (avatar is null || avatar.Length == 0)
        {
            await LoadModelAsync(studentId);
            PhotoError = "Please choose a photo to upload.";
            return Page();
        }

        var extension = Path.GetExtension(avatar.FileName);
        if (!AllowedPhotoExtensions.Contains(extension) ||
            string.IsNullOrWhiteSpace(avatar.ContentType) ||
            !AllowedPhotoMimes.Contains(avatar.ContentType))
        {
            await LoadModelAsync(studentId);
            PhotoError = "Photo must be a JPG, PNG, WebP, or GIF image.";
            return Page();
        }

        if (avatar.Length > MaxPhotoBytes)
        {
            await LoadModelAsync(studentId);
            PhotoError = "Photo must be 5 MB or smaller.";
            return Page();
        }

        var student = await _provisioning.GetByIdAsync(studentId);
        if (student is null)
        {
            await LoadModelAsync(studentId);
            PhotoError = "Your account could not be found. Please sign in again.";
            return Page();
        }

        // The filename is GUID-keyed from the auth claim — never from user input (R4).
        var fileName = $"{studentId:N}{extension.ToLowerInvariant()}";
        var newUrl = $"/avatars/{fileName}";
        var avatarsDir = Path.Combine(_environment.WebRootPath, "avatars");

        try
        {
            Directory.CreateDirectory(avatarsDir);
            var targetPath = Path.Combine(avatarsDir, fileName);

            // Temp file → move: a failed upload never leaves a half-written avatar.
            var tempPath = Path.Combine(avatarsDir, $".{fileName}.{Guid.NewGuid():N}.tmp");
            await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew))
            {
                await avatar.CopyToAsync(fileStream);
            }
            if (System.IO.File.Exists(targetPath))
                System.IO.File.Delete(targetPath);
            System.IO.File.Move(tempPath, targetPath);

            var updated = await _provisioning.UpdateAsync(studentId, null, null, null, newUrl);

            // The column now points at the new file — safe to drop a replaced one
            // (only when the URL changed, e.g. a different extension).
            if (!string.IsNullOrWhiteSpace(student.AvatarPath) && student.AvatarPath != newUrl)
                DeleteAvatarFile(student.AvatarPath);

            await _cookieRefresher.RefreshAsync(HttpContext, updated);

            // Post-Redirect-Get (as the name save): the re-issued cookie carries the
            // new AvatarPath claim, which the nav renders on the resulting GET (FR-009).
            TempData[SuccessTempKey] = "Profile photo updated.";
            return RedirectToPage();
        }
        catch (Exception)
        {
            // Friendly error; the previous photo (file + column) is untouched.
            // Best-effort: drop the orphaned new file if the DB update failed after the move.
            try
            {
                var current = await _provisioning.GetByIdAsync(studentId);
                if (current?.AvatarPath != newUrl)
                {
                    var targetPath = Path.Combine(avatarsDir, fileName);
                    if (System.IO.File.Exists(targetPath))
                        System.IO.File.Delete(targetPath);
                }
            }
            catch (Exception)
            {
                // Cleanup is best-effort; never mask the original failure.
            }

            await LoadModelAsync(studentId);
            PhotoError = "Sorry, the photo could not be saved. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Resend the verification link in place (R8) — mirrors LoginModel.OnPostResendAsync:
    /// neutral result, existing 3/hour-per-email throttle.
    /// </summary>
    public async Task<IActionResult> OnPostResendAsync()
    {
        var studentId = GetStudentId();
        var student = await _provisioning.GetByIdAsync(studentId);
        var email = student?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var result = await _registrationService.ResendVerificationAsync(email, baseUrl);

        await LoadModelAsync(studentId);
        ResendSucceeded = result.Succeeded;
        ResendMessage = result.Succeeded
            ? $"A verification email has been sent to {email}."
            : result.Error;
        return Page();
    }

    // ── Shared load (fresh personal state + the My Courses join, R6) ───────────

    private async Task LoadModelAsync(Guid studentId)
    {
        SuccessMessage = null;
        NameError = null;
        PhotoError = null;
        VerificationRequired = false;
        ResendMessage = null;
        CoursesError = null;
        CompletedCourses = new List<CourseRow>();
        EnrolledCourses = new List<CourseRow>();

        var student = await _provisioning.GetByIdAsync(studentId);
        if (student is not null)
        {
            Name = student.Name;
            Email = student.Email;
            IsEmailVerified = student.IsEmailVerified;
            AvatarUrl = student.AvatarPath;
        }

        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        RoleLabel = roles.Count > 0 ? string.Join(", ", roles) : "Learner";

        await LoadCoursesAsync(studentId);
    }

    /// <summary>
    /// The MyCourses join (R6): enrollments × attempts. FR-006 — a course is
    /// Completed when it has at least one attempt with status completed/passed
    /// (a retake never loses that); everything else is Enrolled, labeled with the
    /// latest attempt's display label (or "Not Started" when there is no attempt).
    /// A load failure sets CoursesError in the courses area only (FR-014).
    /// </summary>
    private async Task LoadCoursesAsync(Guid studentId)
    {
        try
        {
            var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(studentId);
            var attempts = await _scormAttemptService.GetMyAttemptsAsync(studentId);

            var completedByCourse = attempts
                .Where(a => IsCompletedStatus(a.Status))
                .GroupBy(a => a.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptNumber).First());

            var latestByCourse = attempts
                .GroupBy(a => a.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptNumber).First());

            foreach (var (enrollment, courseTitle) in enrollments)
            {
                if (completedByCourse.TryGetValue(enrollment.CourseId, out var completedAttempt))
                {
                    // Completed wins; label with the attempt that established completion.
                    CompletedCourses.Add(new CourseRow(courseTitle, ScormHelpers.GetDisplayLabel(completedAttempt.Status)));
                }
                else
                {
                    var label = latestByCourse.TryGetValue(enrollment.CourseId, out var latest)
                        ? ScormHelpers.GetDisplayLabel(latest.Status)
                        : ScormHelpers.GetDisplayLabel(null); // "Not Started"
                    EnrolledCourses.Add(new CourseRow(courseTitle, label));
                }
            }
        }
        catch (Exception)
        {
            CoursesError = "Sorry, your courses could not be loaded. Please try again.";
        }
    }

    /// <summary>Delete the on-disk file behind an avatar URL path, refusing to touch
    /// anything outside wwwroot/avatars (defense in depth — the value is only ever
    /// written by this page's photo handler).</summary>
    private void DeleteAvatarFile(string avatarUrl)
    {
        try
        {
            var relative = avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var avatarsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "avatars"));
            var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relative));
            if (!fullPath.StartsWith(avatarsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception)
        {
            // A stale file is cosmetic at dev scale; never fail the request over it.
        }
    }

    private static bool IsCompletedStatus(string? status) =>
        status != null && status.ToLowerInvariant() is "completed" or "passed";

    private Guid GetStudentId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id))
            return id;
        return Guid.Empty;
    }
}
