using System.IO.Compression;
using System.Text;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Domain;
using LibreLms.Modules.Scorm.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Scorm.Tests;

/// <summary>
/// Spec 049 — zip-slip traversal protection for <c>ScormPackageService.UploadAsync</c>.
///
/// The extraction loop builds <c>Path.Combine(contentFullPath, entry.FullName)</c>
/// from the attacker-controlled entry name. These tests build in-memory zips whose
/// entries escape the content directory (relative <c>../</c>, a traversal directory
/// entry, and a rooted absolute name) and assert the upload is rejected as a whole,
/// nothing is written outside the content directory, and no partial content
/// directory is left behind.
///
/// RED-VERIFIED against the pre-fix code (see tasks.md Verification Notes).
/// House pattern: real MSSQL via ConnectionStrings__Sql, temp dir as wwwroot.
/// </summary>
public class ScormUploadTraversalTests : IAsyncLifetime
{
    private string _sqlConn = null!;

    public async Task InitializeAsync()
    {
        _sqlConn = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
            ?? throw new InvalidOperationException("ConnectionStrings__Sql environment variable is required.");

        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // A minimal SCORM 1.2 manifest that ManifestParser accepts
    // (resources → resource with href + adlcp:scorm="sco").
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <manifest identifier="TESTPKG" version="1"
                  xmlns="http://www.adlnet.org/xsd/adlcp_v1p2"
                  xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_v1p2"
                  xmlns:imsmd="http://www.imsglobal.org/xsd/imsmd_v1p2">
          <metadata>
            <imsmd:lom>
              <imsmd:general>
                <imsmd:title><imsmd:string>Traversal Test Package</imsmd:string></imsmd:title>
              </imsmd:general>
            </imsmd:lom>
          </metadata>
          <organizations default="ORG">
            <organization identifier="ORG">
              <item identifier="ITEM" identifierref="SCO"/>
            </organization>
          </organizations>
          <resources>
            <resource identifier="SCO" href="index.html" type="webcontent" adlcp:scorm="sco">
              <file href="index.html"/>
            </resource>
          </resources>
        </manifest>
        """;

    [Fact]
    public async Task FileEntryWithDotDot_IsRejected_AndWritesNothingOutsideContentDir()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot);
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                ("../escape.txt", "pwned"));

            var (package, error) = await Upload(service, zip);

            Assert.NotNull(error);
            Assert.Null(package);
            Assert.False(File.Exists(Path.Combine(wwwRoot, "escape.txt")),
                "traversal file was written outside the content directory");
            Assert.True(Directory.GetDirectories(Path.Combine(wwwRoot, "scorm-content")).Length == 0,
                "partial content directory was left behind");
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
        }
    }

    [Fact]
    public async Task DirectoryEntryWithDotDot_IsRejected_AndCreatesNoDirectoryOutsideContentDir()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot);
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                ("../evil-dir/", null), // directory entry — the handoff's specific hazard
                ("../evil-dir/inner.txt", "pwned"));

            var (package, error) = await Upload(service, zip);

            Assert.NotNull(error);
            Assert.Null(package);
            Assert.False(Directory.Exists(Path.Combine(wwwRoot, "evil-dir")),
                "traversal directory was created outside the content directory");
            Assert.False(File.Exists(Path.Combine(wwwRoot, "evil-dir", "inner.txt")));
            Assert.True(Directory.GetDirectories(Path.Combine(wwwRoot, "scorm-content")).Length == 0,
                "partial content directory was left behind");
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
        }
    }

    [Fact]
    public async Task RootedEntryName_IsRejected_AndWritesNothingOutsideContentDir()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        // Unique rooted path so the red run (pre-fix) never clobbers anything real.
        var rootedDir = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, $"scorm-escape-{Guid.NewGuid():N}");
        var rootedFile = Path.Combine(rootedDir, "evil.txt");
        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot);
            // Forward slashes: Path.IsPathRooted("C:/x") is true on Windows, so
            // Path.Combine discards the base directory for this entry name.
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                (rootedFile.Replace('\\', '/'), "pwned"));

            var (package, error) = await Upload(service, zip);

            Assert.NotNull(error);
            Assert.Null(package);
            Assert.False(File.Exists(rootedFile),
                "rooted entry was written to an absolute path outside the content directory");
            Assert.True(Directory.GetDirectories(Path.Combine(wwwRoot, "scorm-content")).Length == 0,
                "partial content directory was left behind");
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
            DeleteQuiet(rootedDir);
        }
    }

    [Fact]
    public async Task UncompressedSizeCap_IsRejected_AndCleansUpPartialDirectory()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        try
        {
            // Tiny cap via the constructor: a few KB of real bytes exceed it.
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot,
                maxEntryCount: ScormPackageService.DefaultMaxEntryCount,
                maxUncompressedBytes: 1_000);
            var bigPayload = new string('x', 4_096);
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                ("content/big.txt", bigPayload));

            var (package, error) = await Upload(service, zip);

            Assert.NotNull(error);
            Assert.Contains("uncompressed size", error, StringComparison.OrdinalIgnoreCase);
            Assert.Null(package);
            Assert.True(Directory.GetDirectories(Path.Combine(wwwRoot, "scorm-content")).Length == 0,
                "partial content directory was left behind after size-cap rejection");
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
        }
    }

    [Fact]
    public async Task EntryCountCap_IsRejected_BeforeAnythingIsWritten()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot,
                maxEntryCount: 2, // manifest + one file already exceeds 2
                maxUncompressedBytes: ScormPackageService.DefaultMaxUncompressedBytes);
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                ("content/a.txt", "a"),
                ("content/b.txt", "b"));

            var (package, error) = await Upload(service, zip);

            Assert.NotNull(error);
            Assert.Contains("entries", error, StringComparison.OrdinalIgnoreCase);
            Assert.Null(package);
            // The count cap rejects before the content directory is created.
            Assert.False(Directory.Exists(Path.Combine(wwwRoot, "scorm-content")),
                "content directory was created despite the entry-count rejection");
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
        }
    }

    [Fact]
    public async Task LegitimateNestedPackage_StillExtractsAndCreatesPackageRow()
    {
        await using var ctx = NewContext();
        var wwwRoot = NewWwwRoot();
        try
        {
            var service = new ScormPackageService(ctx, new ManifestParser(), wwwRoot);
            var zip = BuildZip(
                ("imsmanifest.xml", Manifest),
                ("content/index.html", "<html>test</html>"),
                ("content/img/logo.png", new string('p', 64)),
                ("data/", null)); // nested directory entry — legitimate structure

            var (package, error) = await Upload(service, zip);

            Assert.Null(error);
            Assert.NotNull(package);
            var contentRoot = Path.Combine(wwwRoot, package!.ContentDirectory);
            Assert.True(File.Exists(Path.Combine(contentRoot, "imsmanifest.xml")));
            Assert.True(File.Exists(Path.Combine(contentRoot, "content", "index.html")));
            Assert.True(File.Exists(Path.Combine(contentRoot, "content", "img", "logo.png")));
            Assert.True(Directory.Exists(Path.Combine(contentRoot, "data")));
            // The package row exists in the context (cleaned up in finally).
            Assert.Contains(ctx.ScormPackages.Local, p => p.Id == package.Id);
        }
        finally
        {
            CleanupRows(ctx);
            DeleteQuiet(wwwRoot);
        }
    }

    // ── Helpers ──

    private async Task<(ScormPackage? Package, string? Error)> Upload(
        ScormPackageService service, byte[] zip)
    {
        await using var ms = new MemoryStream(zip);
        return await service.UploadAsync(ms, null);
    }

    private string NewWwwRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"scorm-wwwroot-{Guid.NewGuid():N}")).FullName;

    /// <summary>
    /// Build an in-memory zip. <c>content == null</c> marks a directory entry
    /// (the name is stored verbatim, so it must end with '/').
    /// </summary>
    private static byte[] BuildZip(params (string Name, string? Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                if (content is null)
                    continue;
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    private static void CleanupRows(ScormDbContext ctx)
    {
        try
        {
            var tracked = ctx.ScormPackages.Local.ToList();
            if (tracked.Count > 0)
            {
                ctx.ScormPackages.RemoveRange(tracked);
                ctx.SaveChanges();
            }
        }
        catch
        {
            // Cleanup must never mask the test's own result.
        }
    }

    private static void DeleteQuiet(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best effort.
        }
    }

    private ScormDbContext NewContext(string? connStr = null)
    {
        connStr ??= _sqlConn;
        var hostAssembly = System.Reflection.Assembly.Load("Host");
        var options = new DbContextOptionsBuilder<ScormDbContext>()
            .UseSqlServer(connStr, sql => sql.MigrationsAssembly(hostAssembly))
            .Options;
        return new ScormDbContext(options);
    }
}
