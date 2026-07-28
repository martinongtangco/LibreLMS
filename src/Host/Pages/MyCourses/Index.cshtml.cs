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
    public List<ScormAttempt> ScormAttempts { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Fetch enrollments
        try
        {
            var response = await _httpClient.GetAsync("/api/enrollments/my");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<MyEnrollmentsResponse>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

        // Fetch SCORM attempts
        try
        {
            var attemptResponse = await _httpClient.GetAsync("/api/scorm/attempts/my");
            if (attemptResponse.IsSuccessStatusCode)
            {
                var attemptJson = await attemptResponse.Content.ReadAsStringAsync();
                var attemptData = System.Text.Json.JsonSerializer.Deserialize<ScormAttemptsResponse>(attemptJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (attemptData?.Attempts != null)
                {
                    ScormAttempts = attemptData.Attempts.ToList();
                }
            }
        }
        catch
        {
            // No attempts yet
        }
    }
}

public record MyEnrollmentsResponse(System.Collections.Generic.IEnumerable<MyEnrollment> Enrollments);
public record MyEnrollment(Guid Id, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);

public record ScormAttemptsResponse(System.Collections.Generic.IEnumerable<ScormAttempt> Attempts);
public record ScormAttempt(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    int AttemptNumber,
    string Status,
    double? ScoreRaw,
    string? SessionTime,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset LastCommitAt);
