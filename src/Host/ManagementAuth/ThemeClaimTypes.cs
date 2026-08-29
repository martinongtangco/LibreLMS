namespace LibreLms.Host.ManagementAuth;

/// <summary>Claim type for the user's theme preference carried by the auth cookie (spec 042).
/// Always present — values System | Light | Dark, default/normalized value System — so the
/// shared layout can pick the theme purely from claims, with no service injection or DB access.</summary>
public static class ThemeClaimTypes
{
    public const string ThemePreference = "ThemePreference";
}
