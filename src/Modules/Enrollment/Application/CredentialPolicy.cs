using System.Reflection;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Strict password policy (spec 027 FR-003/FR-004): at least 12 characters; at least one
/// uppercase letter, one lowercase letter and one digit; must not contain the user's full
/// name or email address (case-insensitive); must not be on the top-1000 common-password
/// blocklist. One sentence: evaluates a candidate password and names exactly which rules fail.
/// </summary>
public sealed class CredentialPolicy
{
    private readonly HashSet<string> _blocklist;

    public CredentialPolicy()
    {
        var blocklist = new HashSet<string>();
        // The manifest name is RootNamespace-qualified and dot-separated
        // (e.g. "Enrollment.Resources.common-passwords.txt"), so match on the tail only.
        var resource = typeof(CredentialPolicy).Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".common-passwords.txt", StringComparison.OrdinalIgnoreCase));

        if (resource is not null)
        {
            using var stream = typeof(CredentialPolicy).Assembly.GetManifestResourceStream(resource);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var entry = line.Trim().ToLowerInvariant();
                    if (entry.Length > 0)
                        blocklist.Add(entry);
                }
            }
        }

        _blocklist = blocklist;
    }

    /// <summary>
    /// Evaluate a candidate password. Returns one message per failed rule; an empty list
    /// means the password passes. <paramref name="name"/>/<paramref name="email"/> enable the
    /// name/email-content rules (pass them on every path that knows them).
    /// </summary>
    public IReadOnlyList<string> Evaluate(string password, string? name = null, string? email = null)
    {
        var failures = new List<string>();

        if (password.Length < 12)
            failures.Add("Password must be at least 12 characters.");

        if (!password.Any(char.IsUpper))
            failures.Add("Password must contain at least one uppercase letter.");

        if (!password.Any(char.IsLower))
            failures.Add("Password must contain at least one lowercase letter.");

        if (!password.Any(char.IsDigit))
            failures.Add("Password must contain at least one digit.");

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length > 0 &&
            password.Contains(trimmedName, StringComparison.OrdinalIgnoreCase))
            failures.Add("Password must not contain your full name.");

        var trimmedEmail = email?.Trim() ?? string.Empty;
        if (trimmedEmail.Length > 0 &&
            password.Contains(trimmedEmail, StringComparison.OrdinalIgnoreCase))
            failures.Add("Password must not contain your email address.");

        if (_blocklist.Contains(password.ToLowerInvariant()))
            failures.Add("Password is too common. Choose a different one.");

        return failures;
    }
}
