using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Forgot password (spec 027 US3, FR-014/FR-015): the user submits their email and
/// ALWAYS sees the same neutral confirmation — whether the email is registered,
/// unknown, or the request was throttled. This prevents account enumeration.
/// Registered emails get a 30-minute single-use reset link in the outbox.
/// </summary>
[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly RegistrationService _registrationService;

    [BindProperty] public string Email { get; set; } = string.Empty;

    /// <summary>Set after a POST — the neutral confirmation is shown.</summary>
    public string? Confirmation { get; set; }

    public ForgotPasswordModel(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            Response.Redirect("/Courses");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _registrationService.RequestPasswordResetAsync(Email, baseUrl);
        Confirmation = result.Message;
        return Page();
    }
}
