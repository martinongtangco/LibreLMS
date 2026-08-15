using LibreLms.Host.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Dev;

/// <summary>
/// Developer-only outbox viewer (spec 027 FR-020): shows every email the mock provider
/// "sent" so verification/reset links can be used without a real mailbox.
/// Returns 404 outside the Development environment.
/// </summary>
public class OutboxModel : PageModel
{
    private readonly DevEmailOutbox _outbox;
    private readonly IWebHostEnvironment _environment;

    [BindProperty(SupportsGet = true)]
    public IReadOnlyList<OutboxEntry> Emails { get; set; } = Array.Empty<OutboxEntry>();

    public OutboxModel(DevEmailOutbox outbox, IWebHostEnvironment environment)
    {
        _outbox = outbox;
        _environment = environment;
    }

    public void OnGet()
    {
        if (!_environment.IsDevelopment())
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Response.ContentLength = 0;
            return;
        }

        Emails = _outbox.List();
    }

    /// <summary>POST /Dev/Outbox?handler=Clear — drop all recorded emails.</summary>
    public IActionResult OnPostClear()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        _outbox.Clear();
        return RedirectToPage();
    }

    /// <summary>Split a plain-text body into text/link segments so action links render as clickable anchors.</summary>
    public static IList<(string Text, bool IsLink)> LinkSegments(string body)
    {
        var segments = new List<(string, bool)>();
        var matches = System.Text.RegularExpressions.Regex.Matches(body, @"https?://[^\s]+");
        var last = 0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Index > last)
                segments.Add((body[last..match.Index], false));
            segments.Add((match.Value, true));
            last = match.Index + match.Length;
        }
        if (last < body.Length)
            segments.Add((body[last..], false));
        return segments;
    }
}
