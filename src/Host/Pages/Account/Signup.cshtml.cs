using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Self-service sign-up (spec 027 US1). Anonymous page; signed-in users are sent
/// to the home page. Server re-validates everything (format, strict policy with
/// the specific failed rule(s), confirmation match, case-insensitive duplicate).
/// Success shows a "check your email" confirmation — NO auto sign-in (FR-009).
/// </summary>
[AllowAnonymous]
public class SignupModel : PageModel
{
    private readonly RegistrationService _registrationService;

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Set when the sign-up succeeded — the confirmation screen renders.</summary>
    public bool Succeeded { get; set; }
    public string? GeneralError { get; set; }
    public string? NameError { get; set; }
    public string? EmailError { get; set; }
    public string? PasswordError { get; set; }
    public string? ConfirmError { get; set; }

    public SignupModel(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            Response.Redirect("/");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _registrationService.RegisterAsync(
            Name, Email, Password, ConfirmPassword, baseUrl);

        if (result.Succeeded)
        {
            Succeeded = true;
            return Page();
        }

        GeneralError = result.GeneralError;
        NameError = result.Errors.Name;
        EmailError = result.Errors.Email;
        PasswordError = result.Errors.Password;
        ConfirmError = result.Errors.Confirm;
        return Page();
    }
}
