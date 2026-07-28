using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages.Admin;

public class ScormUploadModel : PageModel
{
    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public Guid? UploadedCourseId { get; set; }

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
            Error = "Invalid course ID. Please enter a valid GUID.";
            return;
        }

        using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(file.OpenReadStream());
        content.Add(streamContent, "package", file.FileName);
        content.Add(new StringContent(courseIdStr ?? ""), "courseId");

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

public record UploadResponse(Guid PackageId, Guid CourseId, string Title, string LaunchPath);
public record ErrorResponse(string Error);
