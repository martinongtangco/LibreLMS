using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Host.Pages.Account;

public class LoginModel : PageModel
{
    private readonly EnrollmentDbContext _enrollmentContext;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public LoginModel(EnrollmentDbContext enrollmentContext, ILogger<LoginModel> logger)
    {
        _enrollmentContext = enrollmentContext;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return Page();
        }

        try
        {
            var student = await _enrollmentContext.Students
                .FirstOrDefaultAsync(s => s.Email == Email);

            if (student is null || !VerifyPassword(Password, student.PasswordHash))
            {
                ErrorMessage = "Invalid email or password.";
                return Page();
            }

            // Build claims principal
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, student.Id.ToString()),
                new(ClaimTypes.Name, student.Name),
                new(ClaimTypes.Email, student.Email)
            };

            // Add role claims if the student has roles
            if (!string.IsNullOrWhiteSpace(student.Roles))
            {
                foreach (var role in student.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // Add OrganizationId claim for org-scoped authorization (T043)
            claims.Add(new Claim(OrgClaimTypes.OrganizationId, student.OrganizationId.ToString()));

            var claimsIdentity = new ClaimsIdentity(claims, "Cookie");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                "Cookie",
                claimsPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            return Redirect("/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for email={Email}", Email);
            ErrorMessage = "An error occurred during login. Please try again.";
            return Page();
        }
    }

    private static bool VerifyPassword(string password, string hash)
    {
        using var sha256 = SHA256.Create();
        var computedHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computedHash == hash;
    }
}
