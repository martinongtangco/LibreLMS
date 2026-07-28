using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LearningLms.Host.Pages;

public class ErrorModel : PageModel
{
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        ErrorMessage = "An unexpected error occurred.";
    }
}
