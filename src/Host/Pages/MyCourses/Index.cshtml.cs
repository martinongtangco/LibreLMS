using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages.MyCourses;

public class MyCoursesModel : PageModel
{
    private readonly HttpClient _httpClient;

    public MyCoursesModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<MyEnrollment> Enrollments { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/enrollments/my");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<MyEnrollmentsResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Enrollments != null)
                {
                    Enrollments = data.Enrollments.ToList();
                }
            }
        }
        catch
        {
            // API call failed — show empty state
        }
    }
}

public record MyEnrollmentsResponse(System.Collections.Generic.IEnumerable<MyEnrollment> Enrollments);
public record MyEnrollment(Guid Id, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);
