namespace LibreLms.Host.ManagementAuth;

/// <summary>Claim type for the SecurityStamp carried by the auth cookie (spec 027 / ADR-0006).
/// Re-validated on each request so a password reset (which rotates the stamp) invalidates
/// all pre-existing sessions.</summary>
public static class SecurityClaims
{
    public const string SecurityStamp = "SecurityStamp";
}
