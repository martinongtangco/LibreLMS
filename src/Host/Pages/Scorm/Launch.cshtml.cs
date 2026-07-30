using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Scorm;

public class ScormLaunchModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _env;

    public ScormLaunchModel(IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
    {
        _httpClient = httpClientFactory.CreateClient();
        _env = env;
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
            var response = await _httpClient.PostAsync($"/api/scorm/{CourseId}/launch", null);
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
        catch
        {
            Error = "An error occurred while launching the course.";
        }
    }
}

public record LaunchResponse(string SessionId, string? ContentUrl, string Entry, int AttemptNumber);
public record ErrorResponse(string Error);
