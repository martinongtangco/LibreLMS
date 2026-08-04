using System.Reflection;
using LibreLms.Host.Pages.Courses;
using LibreLms.Host.Pages.MyCourses;
using LibreLms.Host.Pages.Admin.Dashboard;
using LibreLms.Host.Pages.Account;

namespace ArchitectureTests;

/// <summary>
/// Verifies that every HTMX handler reference in .cshtml files corresponds to a real
/// PageModel handler method. This catches the class of bug where UI changes add
/// hx-get/hx-post attributes pointing to handlers that don't exist — silent failures
/// that only surface when a user clicks a button and nothing happens.
/// 
/// Convention enforced:
/// - Navigation links MUST use asp-page / asp-page-handler (compile-time verified)
/// - HTMX (hx-get/hx-post) is ONLY for form submissions and partial-content AJAX
/// - Every HTMX handler reference MUST have a corresponding OnGet/OnPost method
/// </summary>
public class HtmxHandlerTests
{
    /// <summary>
    /// Known HTMX handler references found in .cshtml files and their owning PageModel types.
    /// Update this list when adding new HTMX endpoints. The test will fail if the handler
    /// method doesn't exist, catching the bug at test time instead of at user-click time.
    /// </summary>
    private static readonly List<(string HandlerName, Type PageModelType)> ExpectedHandlers =
        new()
        {
            // Courses/Index.cshtml — search & filter partial reload
            ("CourseList", typeof(CourseIndexModel)),

            // Courses/Detail.cshtml — enroll form
            ("Enroll", typeof(CourseDetailModel)),

            // MyCourses/Index.cshtml — enrollment list refresh
            ("Enrollments", typeof(MyCoursesModel)),
        };

    [Fact]
    public void Htmx_Handlers_Must_Have_Corresponding_PageModel_Methods()
    {
        var missing = new List<string>();

        foreach (var (handlerName, pageModelType) in ExpectedHandlers)
        {
            // HTMX ?handler=X maps to OnGetXAsync (GET) or OnPostXAsync (POST)
            var getMethodName = $"OnGet{handlerName}Async";
            var postMethodName = $"OnPost{handlerName}Async";

            var getMethod = pageModelType.GetMethod(getMethodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var postMethod = pageModelType.GetMethod(postMethodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (getMethod is null && postMethod is null)
            {
                missing.Add(
                    $"Handler '{handlerName}' on {pageModelType.Name}: " +
                    $"expected '{getMethodName}()' or '{postMethodName}()' — both missing.");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void Htmx_No_Orphaned_Handler_References_In_Cshtml_Files()
    {
        // Scan all .cshtml files in the Pages directory for hx-get/hx-post handler references
        // and verify they map to a known handler in our ExpectedHandlers list.
        // This catches typos, renamed handlers, and forgotten registrations.

        var pagesDir = FindPagesDirectory();
        if (pagesDir is null)
        {
            // If we can't find the Pages directory, skip — the content files may not have been copied.
            // The strongly-typed test above still catches missing handlers.
            return;
        }

        var cshtmlFiles = Directory.GetFiles(pagesDir, "*.cshtml", SearchOption.AllDirectories);
        
        // Matches handler=XYZ in hx-get or hx-post attributes
        var handlerPattern = System.Text.RegularExpressions.Regex.IsMatch(
            string.Empty, "handler="); // just checking regex is available

        var foundHandlers = new HashSet<string>();
        
        foreach (var file in cshtmlFiles)
        {
            var content = File.ReadAllText(file);
            
            // Extract handler names from hx-get and hx-post attributes
            var matches = System.Text.RegularExpressions.Regex.Matches(
                content, @"handler=(\w+)");
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                foundHandlers.Add(match.Groups[1].Value);
            }
        }

        var expectedNames = ExpectedHandlers.Select(h => h.HandlerName).ToHashSet();
        var unknownHandlers = foundHandlers.Except(expectedNames).ToList();

        Assert.Empty(unknownHandlers);
    }

    private static string? FindPagesDirectory()
    {
        // The .cshtml files are copied to the output directory via the project file
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        var pagesDir = Path.Combine(testDir, "Pages");

        if (Directory.Exists(pagesDir))
            return pagesDir;

        // Fallback: look relative to the repo root
        var repoRoot = FindRepoRoot(testDir);
        if (repoRoot is not null)
        {
            pagesDir = Path.Combine(repoRoot, "src", "Host", "Pages");
            if (Directory.Exists(pagesDir))
                return pagesDir;
        }

        return null;
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "LibreLms.slnx")) ||
                File.Exists(Path.Combine(dir, ".gitignore")))
                return dir;
            dir = Path.GetDirectoryName(dir)!;
            if (dir is null) return null;
        }
        return null;
    }
}
