using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LibreLms.Modules.Scorm.Application;

namespace LibreLms.Host.Pages.Admin;

[Authorize(Roles = "SuperUser,OrgAdmin")]
public class ScormUploadModel : PageModel
{
    private readonly ScormPackageService _scormService;

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    /// <summary>Available (unassociated) SCORM packages in the pool.</summary>
    public List<ScormPoolItem> AvailablePackages { get; set; } = new();

    public ScormUploadModel(ScormPackageService scormService)
    {
        _scormService = scormService;
    }

    public async Task OnGetAsync()
    {
        var available = await _scormService.ListAvailableAsync();
        AvailablePackages = available.Select(p => new ScormPoolItem(p.Id, p.ManifestTitle, p.CreatedAt.ToString("yyyy-MM-dd"))).ToList();
    }

    public async Task OnPostAsync(IFormCollection form)
    {
        var file = form.Files.GetFile("package");

        if (file is null || file.Length == 0)
        {
            Error = "No file selected.";
            await OnGetAsync();
            return;
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            Error = "SCORM file must be under 50MB.";
            await OnGetAsync();
            return;
        }

        using var stream = file.OpenReadStream();
        var (package, uploadError) = await _scormService.UploadAsync(stream, null);

        if (uploadError is not null)
        {
            Error = $"Upload failed: {uploadError}";
        }
        else
        {
            SuccessMessage = $"SCORM package '{package!.ManifestTitle}' uploaded and added to the available pool.";
        }

        await OnGetAsync();
    }

    public async Task OnPostDeleteAsync(Guid packageId)
    {
        try
        {
            await _scormService.DeleteAsync(packageId);
            SuccessMessage = "SCORM package deleted.";
        }
        catch (Exception ex)
        {
            Error = $"Failed to delete package: {ex.Message}";
        }
        await OnGetAsync();
    }
}

public record ScormPoolItem(Guid Id, string ManifestTitle, string CreatedAt);
