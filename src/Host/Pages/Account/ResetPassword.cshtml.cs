using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

/// <summary>
/// Reset password (spec 027 US3, FR-016/FR-017): the reset link is the credential —
/// GET peeks at the token state without consuming it (invalid/expired/already-used →
/// friendly error + "request a new reset"); a pending token renders the new-password
/// form. POST consumes the token: the new password is policy-checked, stored as
/// PBKDF2, and the SecurityStamp is rotated (all existing sessions die, FR-018).
/// </summary>
[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly RegistrationService _registrationService;

    [BindProperty] public string? Token { get; set; }
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public bool TokenPending { get; set; }
    public string? StatusTitle { get; set; }
    public string? StatusBody { get; set; }
    public string? PasswordError { get; set; }
    public string? ConfirmError { get; set; }
    public bool Succeeded { get; set; }

    public ResetPasswordModel(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>
    /// GET renders based on the token's state. The token arrives as an explicit
    /// handler parameter: this app's [BindProperty] does not pick it up from the
    /// query string on a parameterless OnGet (verified empirically — see the Verify
    /// page, which uses the same explicit-parameter pattern).
    /// </summary>
    public async Task OnGet(string? token)
    {
        Token = token;
        await RenderByTokenStateAsync(token);
    }

    /// <summary>Shared state machine for GET (peek) and failed POST (re-check).</summary>
    private async Task RenderByTokenStateAsync(string? token)
    {
        var state = await _registrationService.CheckResetTokenAsync(token ?? string.Empty);
        if (state == RegistrationService.ResetTokenState.Pending)
        {
            TokenPending = true;
            return;
        }

        (StatusTitle, StatusBody) = state switch
        {
            RegistrationService.ResetTokenState.Expired =>
                ("Link expired", "This password-reset link has expired (links are valid for 30 minutes). Request a new one."),
            RegistrationService.ResetTokenState.AlreadyUsed =>
                ("Link already used", "This password-reset link has already been used. Request a new one."),
            _ =>
                ("Link missing or invalid", "The reset link is missing or malformed. Please use the link from your password-reset email."),
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (NewPassword != ConfirmPassword)
            ConfirmError = "Passwords do not match.";

        var result = await _registrationService.ResetPasswordAsync(Token ?? string.Empty, NewPassword);

        if (result.Status == RegistrationService.ResetOutcome.Success)
        {
            Succeeded = true;
            return Page();
        }

        // Failure: keep any policy message on the form (token still pending → the
        // state machine re-renders the form) or show the token error page (token
        // invalid/expired/used).
        PasswordError = result.PasswordError;
        await RenderByTokenStateAsync(Token);
        return Page();
    }
}
