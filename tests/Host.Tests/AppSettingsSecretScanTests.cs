namespace Host.Tests;

/// <summary>
/// Spec 051 — secret-scan regression guard: no tracked appsettings*.json in the
/// Host project may carry a credential.
///
/// The original defect (item 3 of the hardening loop): appsettings.Development.json
/// was committed with the live MSSQL SA password — the exact leak the gitignored
/// .env + .env.example pattern exists to prevent. This test fails the build if a
/// connection string with an embedded Password= ever lands back in a tracked
/// settings file. The value belongs in the environment (compose / shell export).
/// </summary>
public class AppSettingsSecretScanTests
{
    [Fact]
    public void Tracked_appsettings_files_contain_no_connection_string_credentials()
    {
        var repoRoot = FindRepoRoot();
        var files = new[]
        {
            Path.Combine(repoRoot, "src", "Host", "appsettings.json"),
            Path.Combine(repoRoot, "src", "Host", "appsettings.Development.json"),
        };

        var offenders = new List<string>();
        foreach (var file in files)
        {
            Assert.True(File.Exists(file), $"missing settings file: {file}");
            // Same leniency as ASP.NET Core's JsonConfigurationProvider
            // (comments + trailing commas are legal in appsettings files).
            var jsonOptions = new System.Text.Json.JsonDocumentOptions
            {
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file), jsonOptions);
            if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrs))
                continue;

            foreach (var prop in connStrs.EnumerateObject())
            {
                var value = prop.Value.ToString();
                if (value.Contains("Password=", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{Path.GetRelativePath(repoRoot, file)} → ConnectionStrings:{prop.Name}");
            }
        }

        Assert.True(offenders.Count == 0,
            "connection strings with embedded credentials must not be tracked — source them from the environment (see spec 051):\n"
            + string.Join("\n", offenders));
    }

    /// <summary>Repo root = the ancestor directory containing .specify.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".specify")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate the repo root (no .specify directory above the test base dir)");
    }
}
