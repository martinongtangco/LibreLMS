using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Scorm;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;

namespace LibreLms.Modules.Scorm.Application;

/// <summary>
/// Service for SCORM package operations: upload, manifest parsing, content extraction.
/// Implements IScormPackageService (Scorm.Contracts) for cross-module access per Constitution Principle III.
/// </summary>
public class ScormPackageService : IScormPackageService
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

    /// <summary>Contract: get package as object for cross-module callers.</summary>
    Task<object?> IScormPackageService.GetPackageByCourseIdAsync(Guid courseId)
        => Task.FromResult<object?>(GetPackageByCourseIdAsync(courseId).Result);

    /// <summary>List SCORM packages not yet associated with any course (available pool).</summary>
    public async Task<IEnumerable<ScormPackage>> ListAvailableAsync()
    {
        return await _context.ScormPackages
            .Where(p => p.CourseId == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Contract: list available as object[] for cross-module callers.</summary>
    Task<IEnumerable<object>> IScormPackageService.ListAvailableAsync()
        => Task.FromResult<IEnumerable<object>>(ListAvailableAsync().Result.Cast<object>());

    /// <summary>Associate an available (unassociated) SCORM package with a course.</summary>
    public async Task AssociateWithCourseAsync(Guid packageId, Guid courseId)
    {
        var package = await _context.ScormPackages.FindAsync(packageId);
        if (package is null)
            throw new KeyNotFoundException($"SCORM package {packageId} not found.");
        if (package.CourseId != null)
            throw new InvalidOperationException("SCORM package is already associated with a course.");

        package.CourseId = courseId;
        await _context.SaveChangesAsync();
    }

    /// <summary>Replace the SCORM package for a course: delete old content and upload new.</summary>
    public async Task<(ScormPackage? Package, string? Error)> ReplacePackageAsync(Guid courseId, Stream zipStream)
    {
        // Delete existing package and its content
        var existing = await GetPackageByCourseIdAsync(courseId);
        if (existing is not null)
        {
            DeleteContentDirectory(existing.ContentDirectory);
            _context.ScormPackages.Remove(existing);
            await _context.SaveChangesAsync();
        }

        // Upload new package
        return await UploadAsync(zipStream, courseId);
    }

    /// <summary>Contract: replace package with object return for cross-module callers.</summary>
    async Task<(object? Package, string? Error)> IScormPackageService.ReplacePackageAsync(Guid courseId, Stream zipStream)
    {
        var result = await ReplacePackageAsync(courseId, zipStream);
        return (result.Package, result.Error);
    }

    /// <summary>Check if a course has an associated SCORM package.</summary>
    public async Task<bool> HasPackageAsync(Guid courseId)
    {
        return await _context.ScormPackages.AnyAsync(p => p.CourseId == courseId);
    }

    /// <summary>Get the content directory path for a course's SCORM package, if any.</summary>
    public async Task<string?> GetContentDirectoryAsync(Guid courseId)
    {
        var package = await GetPackageByCourseIdAsync(courseId);
        return package?.ContentDirectory;
    }

    /// <summary>Delete the SCORM package associated with a course (entity + content directory).</summary>
    public async Task DeletePackageForCourseAsync(Guid courseId)
    {
        var package = await GetPackageByCourseIdAsync(courseId);
        if (package is not null)
        {
            DeleteContentDirectory(package.ContentDirectory);
            _context.ScormPackages.Remove(package);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>Contract: upload with object return for cross-module callers.</summary>
    async Task<(object? Package, string? Error)> IScormPackageService.UploadAsync(Stream zipStream, Guid? courseId)
    {
        var result = await UploadAsync(zipStream, courseId);
        return (result.Package, result.Error);
    }

    /// <summary>Delete a SCORM package and its content directory.</summary>
    public async Task DeleteAsync(Guid packageId)
    {
        var package = await _context.ScormPackages.FindAsync(packageId);
        if (package is null)
            throw new KeyNotFoundException($"SCORM package {packageId} not found.");

        DeleteContentDirectory(package.ContentDirectory);
        _context.ScormPackages.Remove(package);
        await _context.SaveChangesAsync();
    }

    /// <summary>Upload a SCORM ZIP package without associating it with a course (adds to available pool).</summary>
    public async Task<(ScormPackage? Package, string? Error)> UploadAsync(Stream zipStream, Guid? courseId)
    {
        // If courseId is provided, check for existing package
        if (courseId.HasValue)
        {
            var existing = await GetPackageByCourseIdAsync(courseId.Value);
            if (existing is not null)
                return (null, "A SCORM package already exists for this course.");
        }

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

    private void DeleteContentDirectory(string contentDirectory)
    {
        var fullPath = Path.Combine(_wwwRootPath, contentDirectory);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }
    }

    /// <summary>Find the launch path for a course (content directory + launch file).</summary>
    public async Task<string?> FindLaunchPath(Guid courseId)
    {
        var package = await GetPackageByCourseIdAsync(courseId);
        return package is not null ? $"/{package.ContentDirectory}/{package.LaunchPath}" : null;
    }

}
