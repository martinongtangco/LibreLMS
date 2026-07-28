using System.IO.Compression;
using LearningLms.Modules.Scorm.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearningLms.Modules.Scorm.Infrastructure;

/// <summary>
/// Seeds a minimal sample SCORM package for demo purposes.
/// Creates a dummy manifest and content under wwwroot/scorm-content/.
/// </summary>
public static class ScormSeeder
{
    /// <summary>
    /// Seed a sample SCORM package if none exist yet.
    /// </summary>
    public static async Task SeedAsync(ScormDbContext context, string wwwRootPath)
    {
        if (await context.ScormPackages.AnyAsync())
            return;

        var packageId = Guid.NewGuid();
        var contentDir = $"scorm-content/{packageId}";
        var contentFullPath = Path.Combine(wwwRootPath, contentDir);
        Directory.CreateDirectory(contentFullPath);

        // Create a minimal imsmanifest.xml
        var manifestXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <manifest xmlns="http://www.imsglobal.org/xsd/imsmanifest_v1p2"
                      xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_v1p2"
                      identifier="MANIFEST-{0}" version="1.0">
              <metadata>
                <schema>ADL SCORM</schema>
                <schemaversion>2004 3rd Edition</schemaversion>
              </metadata>
              <organizations default="ORG-{0}">
                <organization identifier="ORG-{0}">
                  <title>Sample SCORM Course</title>
                  <item identifier="ITEM-{0}" identifierref="RESOURCE-{0}">
                    <title>Sample SCORM Course</title>
                  </item>
                </organization>
              </organizations>
              <resources>
                <resource identifier="RESOURCE-{0}" type="webcontent" adlcp:scorm="sco" href="index.html">
                  <file>index.html</file>
                </resource>
              </resources>
            </manifest>
            """.Trim();

        var manifestPath = Path.Combine(contentFullPath, "imsmanifest.xml");
        await File.WriteAllTextAsync(manifestPath, manifestXml);

        // Create a minimal index.html
        var indexHtml = """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <title>Sample SCORM Course</title>
                <style>
                    body { font-family: Arial, sans-serif; padding: 2rem; background: #f9f9f9; }
                    h1 { color: #1a1a2e; }
                    p { color: #555; margin: 1rem 0; }
                    .progress { margin: 2rem 0; }
                    button { padding: 0.75rem 1.5rem; background: #1a1a2e; color: white; border: none; border-radius: 4px; cursor: pointer; margin: 0.5rem; }
                    button:hover { background: #16213e; }
                    button.complete { background: #2e7d32; }
                </style>
            </head>
            <body>
                <h1>Sample SCORM Course</h1>
                <p>This is a demo SCORM 1.2 course for testing the Learning LMS platform.</p>
                
                <div class="progress">
                    <h3>Course Progress</h3>
                    <p>Status: <span id="status">not attempted</span></p>
                    <p>Score: <span id="score">0</span>/100</p>
                </div>

                <h3>Actions</h3>
                <button onclick="setProgress()">Set Progress (incomplete)</button>
                <button class="complete" onclick="completeCourse()">Complete Course (score: 85)</button>

                <script>
                    // Set initial status
                    function setProgress() {
                        API.LMSSetValue('cmi.core.lesson_status', 'incomplete');
                        document.getElementById('status').textContent = 'incomplete';
                    }

                    function completeCourse() {
                        API.LMSSetValue('cmi.core.lesson_status', 'completed');
                        API.LMSSetValue('cmi.core.score.raw', '85');
                        document.getElementById('status').textContent = 'completed';
                        document.getElementById('score').textContent = '85';
                        API.LMSCommit();
                    }
                </script>
            </body>
            </html>
            """;

        var indexPath = Path.Combine(contentFullPath, "index.html");
        await File.WriteAllTextAsync(indexPath, indexHtml);

        // We need a courseId to link to. For the demo, we'll create a package
        // that can be linked later. Store it with a placeholder courseId.
        var package = new ScormPackage
        {
            Id = packageId,
            CourseId = Guid.Parse("00000000-0000-0000-0000-000000000000"), // Placeholder — will be linked
            ManifestTitle = "Sample SCORM Course",
            LaunchPath = "index.html",
            ContentDirectory = contentDir,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.ScormPackages.Add(package);
        await context.SaveChangesAsync();
    }
}
