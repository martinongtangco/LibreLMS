using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Admin;

public class ScormUploadModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public Guid? UploadedCourseId { get; set; }

    /// <summary>Available courses for the dropdown selector (US4 - T017).</summary>
    public List<CourseSummary> Courses { get; set; } = new();

    public ScormUploadModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnGetAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        try
        {
            var response = await httpClient.GetAsync("/api/courses");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<CoursesResponse>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Courses = data?.Courses?.ToList() ?? new();
            }
        }
        catch
        {
            // If course listing fails, proceed with empty list
        }
    }

    public async Task OnPostAsync(IFormCollection form)
    {
        var file = form.Files.GetFile("package");
        var courseIdStr = form["courseId"].ToString();

        if (file is null || file.Length == 0)
        {
            Error = "No file selected.";
            return;
        }

        if (!Guid.TryParse(courseIdStr, out var courseId))
        {
            Error = "Invalid course ID. Please select a valid course.";
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file.OpenReadStream());
        content.Add(streamContent, "package", file.FileName);
        content.Add(new StringContent(courseIdStr), "courseId");

        var response = await httpClient.PostAsync("/api/scorm/upload", content);

        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            SuccessMessage = "SCORM package uploaded successfully!";
            var json = await response.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<UploadResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            UploadedCourseId = data?.CourseId;
        }
        else
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            Error = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(errorJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Error
                ?? "Upload failed. Please check the file and try again.";
        }
    }
}

public record CourseSummary(Guid Id, string Title);
public record CoursesResponse(IEnumerable<CourseSummary> Courses);
public record UploadResponse(Guid PackageId, Guid CourseId, string Title, string LaunchPath);
public record ErrorResponse(string Error);
