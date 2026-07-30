using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;

namespace LibreLms.Modules.Scorm.Application;

/// <summary>
/// Service for SCORM package operations: upload, manifest parsing, content extraction.
/// </summary>
public class ScormPackageService
{
    private readonly ScormDbContext _context;
    private readonly ManifestParser _manifestParser;
    private readonly string _wwwRootPath;

    public ScormPackageService(ScormDbContext context, ManifestParser manifestParser, string wwwRootPath)
    {
        _context = context;
        _manifestParser = manifestParser;
        _wwwRootPath = wwwRootPath;
    }

    /// <summary>Get the SCORM package for a course.</summary>
    public async Task<ScormPackage?> GetPackageByCourseIdAsync(Guid courseId)
    {
        return await _context.ScormPackages
            .FirstOrDefaultAsync(p => p.CourseId == courseId);
    }

    /// <summary>Find the launch path for a course (content directory + launch file).</summary>
    public async Task<string?> FindLaunchPath(Guid courseId)
    {
        var package = await GetPackageByCourseIdAsync(courseId);
        return package is not null ? $"/{package.ContentDirectory}/{package.LaunchPath}" : null;
    }

    /// <summary>
    /// Upload a SCORM ZIP package, validate it, extract content, and create a ScormPackage entity.
    /// </summary>
    /// <param name="zipStream">The uploaded ZIP file stream.</param>
    /// <param name="courseId">The course to associate this package with.</param>
    /// <returns>The created ScormPackage, or null with an error message.</returns>
    public async Task<(ScormPackage? Package, string? Error)> UploadAsync(Stream zipStream, Guid courseId)
    {
        // Check for existing package
        var existing = await GetPackageByCourseIdAsync(courseId);
        if (existing is not null)
            return (null, "A SCORM package already exists for this course.");

        // Read ZIP and validate
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var manifestEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.Equals("imsmanifest.xml", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry is null)
            return (null, "Missing imsmanifest.xml in the uploaded package");

        // Parse manifest
        using var manifestStream = manifestEntry.Open();
        var parsed = _manifestParser.Parse(manifestStream);
        if (parsed is null)
            return (null, "Failed to parse imsmanifest.xml — no launchable SCO found.");

        // Extract ZIP to content directory
        var packageId = Guid.NewGuid();
        var contentDir = $"scorm-content/{packageId}";
        var contentFullPath = Path.Combine(_wwwRootPath, contentDir);
        Directory.CreateDirectory(contentFullPath);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                // Directory entry
                var dirPath = Path.Combine(contentFullPath, entry.FullName);
                Directory.CreateDirectory(dirPath);
            }
            else
            {
                var filePath = Path.Combine(contentFullPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                using var stream = entry.Open();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fileStream);
            }
        }

        // Create package entity
        var package = new ScormPackage
        {
            Id = packageId,
            CourseId = courseId,
            ManifestTitle = parsed.Title,
            LaunchPath = parsed.LaunchPath,
            ContentDirectory = contentDir,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ScormPackages.Add(package);
        await _context.SaveChangesAsync();

        return (package, null);
    }
}
