using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibreLms.Host.Pages.Account;

[IgnoreAntiforgeryToken]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync("Cookie");
        return Redirect("/Account/Login");
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync("Cookie");
        return Redirect("/Account/Login");
    }
}
