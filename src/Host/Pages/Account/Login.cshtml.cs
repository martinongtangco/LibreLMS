using System.Security.Claims;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.Host.ManagementAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Login page. Credential checks go through the Enrollment module's
/// RegistrationService (spec 027); the host (composition root) still assembles
/// the auth cookie claims from the Student entity.
///
/// Spec 027 US2 (FR-011): a valid credential for an UNVERIFIED account is
/// rejected with "please check your email" + a resend action — the account
/// cannot sign in until the verification link is used.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    /// <summary>The cookie auth scheme name as registered in Program.cs (AddCookie("Cookie", ...)).</summary>
    private const string AuthScheme = "Cookie";

    private readonly EnrollmentDbContext _context;
    private readonly RegistrationService _registrationService;

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public string? Error { get; set; }

    /// <summary>Set when the credentials were valid but the email is unverified —
    /// the resend form renders with this email pre-filled.</summary>
    public string? UnverifiedEmail { get; set; }

    /// <summary>Set after a resend attempt (success or the neutral "no pending
    /// verification" message).</summary>
    public string? ResendMessage { get; set; }

    /// <summary>Set when an ALREADY-authenticated user reaches the login page —
    /// this is the access-denied bounce (their role lacks permission for the page
    /// they asked for). We show a "signed in but not allowed" state instead of the
    /// form or a redirect: redirecting away would loop (denied → login → home →
    /// …) and hide the reason (see 08-rbac.spec.ts expectations).</summary>
    public bool AccessDenied { get; set; }
    public string? SignedInAs { get; set; }

    public LoginModel(EnrollmentDbContext context, RegistrationService registrationService)
    {
        _context = context;
        _registrationService = registrationService;
    }

    public void OnGet()
    {
        // Already signed in and still on the login page → they were bounced here by
        // an access-denied challenge. Show the denial state (no redirect: that would
        // loop back to the denied page).
        if (User.Identity?.IsAuthenticated == true)
        {
            AccessDenied = true;
            SignedInAs = User.Identity.Name;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            AccessDenied = true;
            SignedInAs = User.Identity?.Name;
            return Page();
        }

        var (success, studentId, error) =
            await _registrationService.VerifyCredentialsAsync(Email, Password);

        if (!success || studentId is null)
        {
            Error = error;
            return Page();
        }

        var student = await _context.Students.FirstAsync(s => s.Id == studentId);

        // FR-011: unverified accounts cannot sign in.
        if (!student.IsEmailVerified)
        {
            UnverifiedEmail = student.Email;
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, student.Id.ToString()),
            new(ClaimTypes.Name, student.Name),
            new(ClaimTypes.Email, student.Email),
            new(SecurityClaims.SecurityStamp, student.SecurityStamp.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(student.Roles))
            claims.Add(new Claim(ClaimTypes.Role, student.Roles));

        var identity = new ClaimsIdentity(claims, AuthScheme);
        await HttpContext.SignInAsync(
            AuthScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect("/");
    }

    /// <summary>
    /// Resend the verification link (spec 027 US2) — offered on the unverified-login
    /// error. Neutral response on purpose; throttled to 3/hour per email.
    /// </summary>
    public async Task<IActionResult> OnPostResendAsync(string email)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _registrationService.ResendVerificationAsync(email, baseUrl);
        ResendMessage = result.Succeeded
            ? $"A verification email has been sent to {email}."
            : result.Error;
        return Page();
    }
}
