using System.Security.Claims;
using LibreLms.Contracts.Enrollment;
using LibreLms.Host.ManagementAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Enrollment.Application;

namespace LibreLms.Host.Pages.Account;

[Authorize]
public class SettingsModel : PageModel
{
    private readonly EnrollmentService _enrollmentService;
    private readonly IUserProvisioning _provisioning;
    private readonly AuthCookieRefresher _cookieRefresher;

    public SettingsModel(
        EnrollmentService enrollmentService,
        IUserProvisioning provisioning,
        AuthCookieRefresher cookieRefresher)
    {
        _enrollmentService = enrollmentService;
        _provisioning = provisioning;
        _cookieRefresher = cookieRefresher;
    }

    [BindProperty]
    public bool EmailNotificationsEnabled { get; set; }

    [BindProperty]
    public string ThemePreference { get; set; } = "System";

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var studentId = GetStudentId();
            var prefs = await _enrollmentService.GetPreferencesAsync(studentId);
            EmailNotificationsEnabled = prefs.EmailNotificationsEnabled;
            ThemePreference = prefs.ThemePreference;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    public async Task OnPostAsync()
    {
        try
        {
            var studentId = GetStudentId();
            await _enrollmentService.UpdatePreferencesAsync(studentId, EmailNotificationsEnabled, ThemePreference);
            SuccessMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    /// <summary>
    /// Spec 042 US1 — AJAX theme save (contracts/theme-ui.md §2). Persists the
    /// preference, re-issues the auth cookie so the ThemePreference claim tracks
    /// the account from the next request, and answers JSON for fetch clients
    /// (plain form POSTs get the standard page re-render fallback).
    /// Anti-forgery is validated implicitly from the form body — never disabled.
    /// </summary>
    public async Task<IActionResult> OnPostThemeAsync()
    {
        try
        {
            var studentId = GetStudentId();
            var theme = string.IsNullOrWhiteSpace(ThemePreference) ? "System"
                : ThemePreference switch { "System" or "Light" or "Dark" => ThemePreference, _ => "System" };

            await _enrollmentService.UpdatePreferencesAsync(studentId, EmailNotificationsEnabled, theme);

            // Re-issue the cookie claim (spec 030 pattern — same seam Profile uses).
            var student = await _provisioning.GetByIdAsync(studentId);
            if (student is not null)
                await _cookieRefresher.RefreshAsync(HttpContext, student);

            if (IsAjaxRequest())
                return new JsonResult(new { success = true, message = (string?)null });

            ThemePreference = theme;
            SuccessMessage = "Settings saved.";
            return Page();
        }
        catch (Exception ex)
        {
            if (IsAjaxRequest())
                return new JsonResult(new { success = false, message = $"Failed to save theme: {ex.Message}" });

            ErrorMessage = $"Failed to save settings: {ex.Message}";
            return Page();
        }
    }

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"].ToString() == "fetch";

    private Guid GetStudentId()
    {
        var claim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var guid))
            return guid;
        return Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
    }
}
