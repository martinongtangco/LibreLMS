using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Dev;

/// <summary>
/// Development-only verification toggle (spec 030 R7): flips the account unverified and re-issues
/// a verification token so the profile verification-gate flow is E2E-observable; 404 outside Development.
/// </summary>
[Authorize]
public class UnverifyModel : PageModel
{
    private readonly RegistrationService _registrationService;
    private readonly IWebHostEnvironment _environment;

    public string Message { get; set; } = string.Empty;

    public UnverifyModel(RegistrationService registrationService, IWebHostEnvironment environment)
    {
        _registrationService = registrationService;
        _environment = environment;
    }

    public async Task OnGetAsync(string? email)
    {
        if (!_environment.IsDevelopment())
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Response.ContentLength = 0;
            return;
        }

        bool flipped = await _registrationService.SetUnverifiedAsync(email ?? string.Empty);
        Message = flipped ? "unverified " + (email ?? string.Empty) : "no account for " + (email ?? string.Empty);
    }
}
