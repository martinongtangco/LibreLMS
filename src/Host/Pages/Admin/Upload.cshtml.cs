using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Management.Application;

namespace LibreLms.Host.Pages.Admin;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class ScormUploadModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CourseCatalogService _catalogService;
    private readonly CourseVisibilityService _visibilityService;

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public Guid? UploadedCourseId { get; set; }

    /// <summary>Available courses for the dropdown selector.</summary>
    public List<CourseSummaryWithOrg> Courses { get; set; } = new();

    public ScormUploadModel(IHttpClientFactory httpClientFactory, CourseCatalogService catalogService, CourseVisibilityService visibilityService)
    {
        _httpClientFactory = httpClientFactory;
        _catalogService = catalogService;
        _visibilityService = visibilityService;
    }

    public async Task OnGetAsync()
    {
        // Load all courses with org info
        var allCourses = await _visibilityService.GetAllCoursesAsync();
        Courses = allCourses.Select(c => new CourseSummaryWithOrg(c.CourseId, c.Title, c.OwningOrganizationName)).ToList();
    }

    public async Task OnPostAsync(IFormCollection form)
    {
        var file = form.Files.GetFile("package");
        var courseIdStr = form["courseId"].ToString();

        if (file is null || file.Length == 0)
        {
            Error = "No file selected.";
            await OnGetAsync();
            return;
        }

        if (!Guid.TryParse(courseIdStr, out var courseId))
        {
            Error = "Invalid course ID. Please select a valid course.";
            await OnGetAsync();
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

        await OnGetAsync();
    }
}

public record CourseSummaryWithOrg(Guid Id, string Title, string OrganizationName);

public record CourseSummary(Guid Id, string Title);
public record CoursesResponse(IEnumerable<CourseSummary> Courses);
public record UploadResponse(Guid PackageId, Guid CourseId, string Title, string LaunchPath);
public record ErrorResponse(string Error);
