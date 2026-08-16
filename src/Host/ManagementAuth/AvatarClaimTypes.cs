namespace LibreLms.Host.ManagementAuth;

/// <summary>Claim type for the display photo carried by the auth cookie (spec 030).
/// Value = the avatar's URL path (e.g. "/avatars/&lt;guid&gt;.png") so the shared layout
/// can render the photo purely from claims, with no service injection or DB access.</summary>
public static class AvatarClaimTypes
{
    public const string AvatarPath = "AvatarPath";
}
