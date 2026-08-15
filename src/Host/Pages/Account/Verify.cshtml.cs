using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Email verification link target (spec 027 FR-012..FR-015). Read-only GET — the
/// link itself is the credential (single-use, 24 h). Result states: success,
/// invalid (missing/malformed/unknown), expired, already used.
/// </summary>
[AllowAnonymous]
public class VerifyModel : PageModel
{
    public bool Success { get; set; }
    public string? ErrorTitle { get; set; }
    public string? ErrorBody { get; set; }

    public async Task OnGet(string? token, [FromServices] RegistrationService registrationService)
    {
        var result = await registrationService.VerifyEmailAsync(token ?? string.Empty);

        switch (result.Status)
        {
            case RegistrationService.VerificationStatus.Success:
                Success = true;
                break;
            case RegistrationService.VerificationStatus.Expired:
                ErrorTitle = "Link expired";
                ErrorBody = "This verification link has expired (links are valid for 24 hours). " +
                            "Sign in and use “Resend verification email” to get a fresh link.";
                break;
            case RegistrationService.VerificationStatus.AlreadyUsed:
                ErrorTitle = "Link already used";
                ErrorBody = "This verification link has already been used.";
                break;
            default:
                ErrorTitle = "Link missing or invalid";
                ErrorBody = "The verification link is missing or malformed. " +
                            "Please use the link from your verification email.";
                break;
        }
    }
}
