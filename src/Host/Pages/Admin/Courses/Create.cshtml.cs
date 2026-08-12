using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Host.ManagementAuth;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Endpoints;
using LibreLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.Admin.Courses;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class CreateCourseModel : PageModel
{
    private readonly CourseCatalogService _catalogService;
    private readonly ScormPackageService _scormService;

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ShortDescription { get; set; } = string.Empty;
    [BindProperty] public string FullDescription { get; set; } = string.Empty;
    [BindProperty] public string Category { get; set; } = string.Empty;
    [BindProperty] public string Duration { get; set; } = string.Empty;

    [BindProperty] public string ScormMode { get; set; } = "none";
    [BindProperty] public IFormFile? ScormFile { get; set; }
    [BindProperty] public Guid? ScormPackageId { get; set; }

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }
    public Guid? CreatedCourseId { get; set; }

    public List<ScormPackageSummary> AvailableScormPackages { get; set; } = new();

    public CreateCourseModel(CourseCatalogService catalogService, ScormPackageService scormService)
    {
        _catalogService = catalogService;
        _scormService = scormService;
    }

    public async Task OnGetAsync()
    {
        var available = await _scormService.ListAvailableAsync();
        AvailableScormPackages = available.Select(p => new ScormPackageSummary(p.Id, p.ManifestTitle, p.CreatedAt.ToString("yyyy-MM-dd"))).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            // Validate SCORM file if upload mode
            if (ScormMode == "upload" && (ScormFile is null || ScormFile.Length == 0))
            {
                Error = "Please select a SCORM ZIP file.";
                await OnGetAsync();
                return Page();
            }
            if (ScormMode == "upload" && ScormFile!.Length > 50 * 1024 * 1024)
            {
                Error = "SCORM file must be under 50MB.";
                await OnGetAsync();
                return Page();
            }

            // Validate SCORM association
            if (ScormMode == "associate" && !ScormPackageId.HasValue)
            {
                Error = "Please select a SCORM package from the dropdown.";
                await OnGetAsync();
                return Page();
            }

            var orgIdStr = User.FindFirstValue(OrgClaimTypes.OrganizationId);
            Guid? orgId = Guid.TryParse(orgIdStr, out var parsedOrgId) ? parsedOrgId : (Guid?)null;

            var course = await _catalogService.CreateAsync(
                new CreateCourseRequest(
                    Title,
                    ShortDescription,
                    FullDescription,
                    Category,
                    Duration,
                    orgId
                ));

            // Handle SCORM
            if (ScormMode == "upload" && ScormFile is not null)
            {
                using var stream = ScormFile.OpenReadStream();
                var (package, uploadError) = await _scormService.UploadAsync(stream, course.Id);
                if (uploadError is not null)
                {
                    Error = $"SCORM upload failed: {uploadError}";
                    await OnGetAsync();
                    return Page();
                }
            }
            else if (ScormMode == "associate" && ScormPackageId.HasValue)
            {
                await _scormService.AssociateWithCourseAsync(ScormPackageId.Value, course.Id);
            }

            CreatedCourseId = course.Id;
            return RedirectToPage("./Index", new { success = true });
        }
        catch (Exception ex)
        {
            Error = $"Failed to create course: {ex.Message}";
            await OnGetAsync();
            return Page();
        }
    }
}

public record ScormPackageSummary(Guid Id, string ManifestTitle, string CreatedAt);
