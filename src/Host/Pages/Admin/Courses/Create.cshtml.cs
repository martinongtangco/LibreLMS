using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages.Admin.Courses;

public class CreateCourseModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ShortDescription { get; set; } = string.Empty;
    [BindProperty] public string FullDescription { get; set; } = string.Empty;
    [BindProperty] public string Category { get; set; } = string.Empty;
    [BindProperty] public string Duration { get; set; } = string.Empty;

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public Guid? CreatedCourseId { get; set; }

    public CreateCourseModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnPostAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();

        var body = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Title = Title,
            ShortDescription = ShortDescription,
            FullDescription = FullDescription,
            Category = Category,
            Duration = Duration
        }, body);

        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("/api/courses", content);

        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<CourseCreatedResponse>(responseJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            CreatedCourseId = data?.Id;
            SuccessMessage = "Course created successfully!";
        }
        else
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            Error = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? "You do not have permission to create courses."
                : $"Failed to create course: {errorJson}";
        }
    }
}

public record CourseCreatedResponse(Guid Id, string Title, string ShortDescription, string Category, string Duration);
