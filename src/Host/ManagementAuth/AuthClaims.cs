using System.Security.Claims;

namespace LibreLms.Host.ManagementAuth;

/// <summary>
/// The single source of truth for the auth cookie's claim set (story 040).
///
/// Both sign-in paths — LoginModel.OnPostAsync (initial sign-in) and
/// AuthCookieRefresher.RefreshAsync (post-profile-change re-issue, spec 030) —
/// MUST build their claims through this method. The failure this prevents:
/// bug-039, where the claim list was duplicated in both builders and the spec 027
/// rebuild dropped the OrganizationId claim from both, silently blanking every
/// OrgAdmin dashboard. With one builder there is nothing to drift.
///
/// The contract is pinned by tests/Host.Tests/AuthClaimsTests.cs — adding,
/// removing, or renaming a claim type must update that test (which fails the
/// build) before it can ship.
/// </summary>
public static class AuthClaims
{
    /// <summary>
    /// Build the full claim set for a sign-in cookie.
    /// Always present: NameIdentifier, Name, Email, SecurityStamp, OrganizationId
    /// (org-scoped authorization — spec 009 T043, restored in bug-039).
    /// Present only when non-empty: Role, AvatarPath (spec 030).
    /// </summary>
    public static List<Claim> Build(
        Guid studentId,
        string name,
        string email,
        Guid securityStamp,
        Guid organizationId,
        string? role = null,
        string? avatarPath = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, studentId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new(SecurityClaims.SecurityStamp, securityStamp.ToString()),
            new(OrgClaimTypes.OrganizationId, organizationId.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (!string.IsNullOrWhiteSpace(avatarPath))
            claims.Add(new Claim(AvatarClaimTypes.AvatarPath, avatarPath));

        return claims;
    }
}
