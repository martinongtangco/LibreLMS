using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages.Courses;

public class CourseDetailModel : PageModel
{
    private readonly HttpClient _httpClient;

    public CourseDetailModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    public CourseDetail? Course { get; set; }
    public bool IsEnrolled { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Fetch course details
            var response = await _httpClient.GetAsync($"/api/courses/{Id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Course = System.Text.Json.JsonSerializer.Deserialize<CourseDetail>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // Check enrollment status (best effort)
            try
            {
                var enrollResponse = await _httpClient.GetAsync("/api/enrollments/my");
                if (enrollResponse.IsSuccessStatusCode)
                {
                    var enrollJson = await enrollResponse.Content.ReadAsStringAsync();
                    var enrollData = System.Text.Json.JsonSerializer.Deserialize<MyEnrollmentsResponse>(enrollJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    IsEnrolled = enrollData?.Enrollments.Any(e => e.CourseId == Id) ?? false;
                }
            }
            catch
            {
                // Enrollment check failed — show enroll button by default
            }
        }
        catch
        {
            // API call failed
        }
    }
}

public record CourseDetail(
    Guid Id, string Title, string ShortDescription, string FullDescription,
    string Category, string Duration, bool IsScorm, Guid? ScormPackageId);

public record MyEnrollmentsResponse(IEnumerable<MyEnrollmentItem> Enrollments);
public record MyEnrollmentItem(Guid Id, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);
