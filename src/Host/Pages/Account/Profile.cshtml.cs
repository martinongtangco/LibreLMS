using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;

    public void OnGet(ClaimsPrincipal user)
    {
        Name = user.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        Email = user.FindFirstValue(ClaimTypes.Email) ?? "";

        var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        RoleLabel = roles.Count > 0 ? string.Join(", ", roles) : "Learner";
    }
}
