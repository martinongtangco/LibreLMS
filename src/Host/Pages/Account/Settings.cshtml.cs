using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Enrollment.Application;

namespace LibreLms.Host.Pages.Account;

[Authorize]
public class SettingsModel : PageModel
{
    private readonly EnrollmentService _enrollmentService;

    public SettingsModel(EnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
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

    private Guid GetStudentId()
    {
        var claim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var guid))
            return guid;
        return Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
    }
}
