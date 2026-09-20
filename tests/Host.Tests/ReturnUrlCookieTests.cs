using LibreLms.Host;
using Microsoft.AspNetCore.Http;

namespace Host.Tests;

/// <summary>
/// Spec 055 — the pending return address (contracts/return-url.md).
///
/// The 24 h cookie that carries the page a signed-out visitor was trying to
/// reach through sign-in, sign-up, and email verification. These tests pin
/// the security-critical pure logic: local-URL-only (open-redirect protection
/// at BOTH set and consume time) and the cookie attributes that keep the
/// value out of JS reach. The cookie's set/carry/consume-over-HTTP behavior
/// is pinned by the E2E journey tests (21-logged-out-enroll.spec.ts, which
/// assert the cookie's presence/absence via the browser's cookie jar).
/// </summary>
public class ReturnUrlCookieTests
{
    private const string CourseUrl = "/Courses/Detail/11111111-1111-1111-1111-111111111115?handler=Enroll";

    // ── local-URL validation (FR-006: open-redirect protection) ───────────

    [Theory]
    [InlineData("/MyCourses")]
    [InlineData("/Courses/Detail/11111111-1111-1111-1111-111111111115?handler=Enroll")]
    [InlineData("/a/b?c=d#frag")]
    [InlineData("/")]
    public void IsLocalUrl_accepts_local_paths_including_query_and_fragment(string localUrl)
    {
        Assert.True(ReturnUrlCookie.IsLocalUrl(localUrl));
    }

    [Theory]
    [InlineData("https://evil.example/")]  // absolute foreign URL
    [InlineData("//evil.example/")]         // protocol-relative (classic open-redirect vector)
    [InlineData("https:evil.example")]      // scheme without slashes
    [InlineData("javascript:alert(1)")]
    [InlineData("foo/bar")]                 // root-relative
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLocalUrl_rejects_everything_that_is_not_a_local_path(string? nonLocal)
    {
        Assert.False(ReturnUrlCookie.IsLocalUrl(nonLocal));
    }

    // ── cookie attributes (data-model.md cookie shape) ─────────────────────

    [Fact]
    public void EncodeValue_url_encodes_the_address()
    {
        Assert.Equal(Uri.EscapeDataString(CourseUrl), ReturnUrlCookie.EncodeValue(CourseUrl));
    }

    [Fact]
    public void BuildCookieOptions_emits_the_contracted_attributes_for_dev_http()
    {
        var options = ReturnUrlCookie.BuildCookieOptions(isHttps: false);

        Assert.True(options.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.Equal(TimeSpan.FromHours(24), options.MaxAge);
        Assert.Equal("/", options.Path);
        Assert.False(options.Secure); // dev runs on plain http://localhost:5000 — must not break
    }

    [Fact]
    public void BuildCookieOptions_is_secure_for_https_hosts()
    {
        var options = ReturnUrlCookie.BuildCookieOptions(isHttps: true);

        Assert.True(options.Secure);
    }

    // ── consume decision (request cookie → honored value or null) ──────────

    [Fact]
    public void Consume_pending_with_a_valid_request_cookie_returns_the_local_url()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = $"{ReturnUrlCookie.CookieName}={Uri.EscapeDataString(CourseUrl)}";

        Assert.Equal(CourseUrl, ReturnUrlCookie.ConsumePending(context));
    }

    [Fact]
    public void Consume_pending_with_no_cookie_returns_null()
    {
        var context = new DefaultHttpContext();

        Assert.Null(ReturnUrlCookie.ConsumePending(context));
    }

    [Theory]
    [InlineData("https%3A%2F%2Fevil.example%2F")] // URL-encoded foreign URL planted in the cookie
    [InlineData("%2F%2Fevil.example%2F")]          // encoded protocol-relative
    [InlineData("javascript%3Aalert(1)")]
    public void Consume_pending_revalidates_and_drops_forged_values(string forgedValue)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = $"{ReturnUrlCookie.CookieName}={forgedValue}";

        Assert.Null(ReturnUrlCookie.ConsumePending(context));
    }
}
