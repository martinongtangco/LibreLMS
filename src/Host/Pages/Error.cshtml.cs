using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages;

public class ErrorModel : PageModel
{
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        ErrorMessage = "An unexpected error occurred.";
    }
}
