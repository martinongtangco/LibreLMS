using System.Security.Cryptography;
using System.Text;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing (spec 027, ADR-0006) with a legacy unsalted-SHA256
/// verify-and-upgrade path so accounts created before spec 027 keep verifying.
/// One sentence: turns a password into a salted, self-describing hash and checks passwords
/// against either the new or the legacy format.
/// </summary>
public sealed class PasswordHasher
{
    /// <summary>OWASP-recommended PBKDF2-HMAC-SHA256 iteration count (spec 027 R2).</summary>
    public const int Pbkdf2Iterations = 210_000;

    /// <summary>Self-describing stored format: PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;.</summary>
    public const string FormatPrefix = "PBKDF2";

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly HashAlgorithmName Sha256 = HashAlgorithmName.SHA256;

    /// <summary>Hash a password with a fresh random 16-byte salt into the self-describing PBKDF2 format.</summary>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, Pbkdf2Iterations, HashSize);
        return $"{FormatPrefix}${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verify a password against a stored hash.
    /// Accepts the self-describing PBKDF2 format (parsed from the stored string, constant-time
    /// comparison) and the legacy unsalted-SHA256-base64 format (what seeders/login used before
    /// spec 027). Returns (Verified, NeedsUpgrade) — NeedsUpgrade is true only when the stored
    /// hash is legacy AND the password matches, so callers can re-hash it in place.
    /// </summary>
    public (bool Verified, bool NeedsUpgrade) Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return (false, false);

        if (storedHash.StartsWith($"{FormatPrefix}$", StringComparison.Ordinal))
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4)
                return (false, false);

            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
                return (false, false);

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return (false, false);
            }

            var actual = Derive(password, salt, iterations, expected.Length);
            return (CryptographicOperations.FixedTimeEquals(actual, expected), false);
        }

        // Legacy format: unsalted SHA-256, base64. Verified legacy passwords should be
        // re-hashed to PBKDF2 by the caller (UpgradeToPbkdf2) to converge the database.
        byte[] expectedLegacy;
        try
        {
            expectedLegacy = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return (false, false);
        }

        using var sha256 = SHA256.Create();
        var actualLegacy = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return (CryptographicOperations.FixedTimeEquals(actualLegacy, expectedLegacy), true);
    }

    /// <summary>Re-hash a known-good password into the PBKDF2 format (upgrade path for legacy hashes).</summary>
    public string UpgradeToPbkdf2(string password) => Hash(password);

    /// <summary>
    /// PBKDF2-HMAC-SHA256 key derivation. Uses Rfc2898DeriveBytes because the
    /// System.Security.Cryptography.KeyDerivation static class (the .NET-recommended API)
    /// is not present in this environment's .NET 10 runtime build; Rfc2898DeriveBytes is
    /// the identical algorithm (see ADR-0006, spec 027 R2).
    /// </summary>
    private static byte[] Derive(string password, byte[] salt, int iterations, int outputLength)
    {
#pragma warning disable SYSLIB0060 // KeyDerivation.Pbkdf2 is the recommended API but is absent from this runtime build.
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, Sha256);
#pragma warning restore SYSLIB0060
        return deriveBytes.GetBytes(outputLength);
    }
}
