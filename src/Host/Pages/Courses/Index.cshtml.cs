using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages.Courses;

public class CourseIndexModel : PageModel
{
    private readonly HttpClient _httpClient;

    public CourseIndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<CourseItem> Courses { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }

    public async Task OnGetAsync()
    {
        var url = "/api/courses";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(Search)) queryParams.Add($"search={Uri.EscapeDataString(Search)}");
        if (!string.IsNullOrWhiteSpace(Category)) queryParams.Add($"category={Uri.EscapeDataString(Category)}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<CourseListResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Courses != null)
                {
                    // Fetch current user's enrollments to determine enrollment status per course
                    var enrolledCourseIds = await GetEnrolledCourseIds();

                    Courses = data.Courses.Select(c => new CourseItem(
                        c.Id, c.Title, c.ShortDescription, c.Category, c.Duration,
                        enrolledCourseIds.Contains(c.Id))).ToList();
                    Categories = Courses.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();
                }
            }
        }
        catch
        {
            // If API call fails, show empty state
        }
    }

    /// <summary>Fetch the set of course IDs the current student is enrolled in.</summary>
    private async Task<HashSet<Guid>> GetEnrolledCourseIds()
    {
        var ids = new HashSet<Guid>();
        try
        {
            var response = await _httpClient.GetAsync("/api/enrollments/my");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<EnrollmentListResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Enrollments != null)
                {
                    foreach (var e in data.Enrollments)
                        ids.Add(e.CourseId);
                }
            }
        }
        catch
        {
            // Best-effort — show courses without enrollment indicators
        }
        return ids;
    }
}

public record EnrollmentListResponse(System.Collections.Generic.IEnumerable<EnrollmentItem> Enrollments);
public record EnrollmentItem(Guid Id, Guid CourseId, string CourseTitle, System.DateTimeOffset EnrolledAt);

public record CourseListResponse(IEnumerable<CourseItem> Courses);
public record CourseItem(Guid Id, string Title, string ShortDescription, string Category, string Duration, bool IsEnrolled = false);
