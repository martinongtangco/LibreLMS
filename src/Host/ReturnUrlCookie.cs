using Microsoft.AspNetCore.Http;

namespace LibreLms.Host;

/// <summary>
/// Spec 055 — the pending return address (contracts/return-url.md, data-model.md).
///
/// The local page a signed-out visitor was trying to reach when a
/// sign-in-required action challenged them. Stored in a 24 h cookie that is
/// set on the sign-in page, carried through sign-up and email verification
/// (neither page touches it), and consumed ONCE by the next successful
/// sign-in. Ephemeral navigation convenience: losing it degrades to landing
/// on the home page — nothing more (Principle VI).
/// </summary>
public static class ReturnUrlCookie
{
    /// <summary>Cookie name for the pending return address.</summary>
    public const string CookieName = "lms.ReturnUrl";

    /// <summary>Retention window (spec FR-007; mirrors the verification-link lifetime).</summary>
    public const int LifetimeHours = 24;

    /// <summary>
    /// A local (same-site) URL starts with a single '/' and is not
    /// protocol-relative ('//') — anything else (absolute foreign URLs,
    /// scheme URLs, root-relative paths) is rejected. This is the
    /// open-redirect gate applied at BOTH set and consume time (FR-006).
    /// </summary>
    public static bool IsLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url![0] == '/'
        && !url.StartsWith("//", StringComparison.Ordinal);

    /// <summary>Cookie values are URL-encoded (the address carries ? and #).</summary>
    public static string EncodeValue(string localUrl) => Uri.EscapeDataString(localUrl);

    /// <summary>
    /// The cookie options for a pending return address: out of JS reach
    /// (HttpOnly), 24 h, Lax, Secure on https hosts only (dev runs plain
    /// http://localhost:5000).
    /// </summary>
    public static CookieOptions BuildCookieOptions(bool isHttps) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromHours(LifetimeHours),
        Secure = isHttps,
    };

    /// <summary>
    /// Store the pending return address from the sign-in page's <c>ReturnUrl</c>
    /// query value. A missing value is a no-op (an existing cookie is left
    /// untouched — this is what preserves the address through the verify →
    /// "Go to sign in" hop); a non-local value is never stored (FR-006).
    /// Repeated sets overwrite (single slot, most-recent-wins, FR-007).
    /// </summary>
    public static void SetPending(HttpContext context, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return;

        if (!IsLocalUrl(returnUrl))
            return;

        context.Response.Cookies.Append(CookieName, EncodeValue(returnUrl), BuildCookieOptions(context.Request.IsHttps));
    }

    /// <summary>
    /// Read and re-validate the pending return address, then delete the cookie
    /// (consume-once, FR-007). Returns the local URL, or null when no cookie is
    /// present or its value fails re-validation (a forged value is deleted,
    /// never honored).
    /// </summary>
    public static string? ConsumePending(HttpContext context)
    {
        var raw = context.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw))
            return null;

        var url = Uri.UnescapeDataString(raw);
        if (!IsLocalUrl(url))
        {
            context.Response.Cookies.Delete(CookieName); // drop the forged value
            return null;
        }

        context.Response.Cookies.Delete(CookieName); // consumed
        return url;
    }
}
