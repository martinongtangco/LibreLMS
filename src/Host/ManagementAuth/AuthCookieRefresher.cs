using System.Security.Claims;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Application;
using Microsoft.AspNetCore.Authentication;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// Re-issues the auth cookie from a fresh Student row (spec 030, R2 "RefreshSignIn"
/// pattern). The cookie embeds the Name at login time, so after a successful profile
/// change (name or photo save) the nav would show stale data until re-login. This
/// helper rebuilds the exact claim list <c>LoginModel.OnPostAsync</c> builds —
/// NameIdentifier, Name, Email, SecurityStamp, Role (when set) — plus the new
/// <c>AvatarPath</c> claim (R3) and signs in again. The claim shape is identical, so
/// the OnValidatePrincipal stamp re-check and role authorization are unaffected.
/// </summary>
public sealed class AuthCookieRefresher
{
    /// <summary>The cookie auth scheme name as registered in Program.cs (AddCookie("Cookie", ...)).</summary>
    private const string AuthScheme = "Cookie";

    private readonly RegistrationService _registrationService;

    public AuthCookieRefresher(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>Re-signs the current request's cookie from the passed account state.
    /// The SecurityStamp is re-read from the DB (one indexed primary-key lookup — the
    /// same cost the cookie's OnValidatePrincipal re-check already pays per request)
    /// so the re-issued cookie always carries the account's current stamp.
    /// The claim set comes from AuthClaims.Build (story 040) — the same single
    /// source LoginModel.OnPostAsync uses, so the two sign-in paths cannot drift
    /// (bug-039: duplicated builders silently dropped the OrganizationId claim).</summary>
    public async Task RefreshAsync(HttpContext context, StudentProvisionedDto student)
    {
        var stamp = await _registrationService.GetSecurityStampAsync(student.Id);

        var claims = AuthClaims.Build(
            student.Id, student.Name, student.Email, stamp ?? Guid.Empty,
            student.OrganizationId, student.Role, student.AvatarPath);

        var identity = new ClaimsIdentity(claims, AuthScheme);
        await context.SignInAsync(
            AuthScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
