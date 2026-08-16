using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Scorm;

[Authorize]
public class ScormLaunchModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ScormLaunchModel> _logger;

    public ScormLaunchModel(IHttpClientFactory httpClientFactory, IWebHostEnvironment env,
        ILogger<ScormLaunchModel> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _env = env;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)] public Guid CourseId { get; set; }
    public string? SessionId { get; set; }
    public string? ContentUrl { get; set; }
    public string? ApiUrl { get; set; }
    public string Entry { get; set; } = "initial";
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // The launch API is same-origin, so build an absolute URI from the incoming
            // request (the app listens on both http://localhost:5000 and
            // https://localhost:7095 — nothing hardcoded). The cookie header carries
            // the caller's session so the API's [Authorize] sees the same student.
            var baseUri = new Uri(Request.Scheme + "://" + Request.Host, UriKind.Absolute);
            var launchUri = new Uri(baseUri, $"/api/scorm/{CourseId}/launch");
            var request = new HttpRequestMessage(HttpMethod.Post, launchUri);
            request.Headers.TryAddWithoutValidation("Cookie", Request.Headers.Cookie.ToString());
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<LaunchResponse>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                {
                    SessionId = data.SessionId;
                    Entry = data.Entry ?? "initial";
                    // Build URLs relative to the base
                    ContentUrl = data.ContentUrl;
                    ApiUrl = $"/api/scorm/session/{SessionId}/api.js";
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Error = "You are not enrolled in this course.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                Error = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(errorJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Error
                    ?? "Failed to launch the SCORM course.";
            }
            else
            {
                Error = "Failed to launch the SCORM course.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SCORM launch failed for course {CourseId}", CourseId);
            Error = "An error occurred while launching the course.";
        }
    }
}

public record LaunchResponse(string SessionId, string? ContentUrl, string Entry, int AttemptNumber);
public record ErrorResponse(string Error);
