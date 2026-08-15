using System.Security.Cryptography;
using System.Text;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Self-service account lifecycle for Student (spec 027): registration, email
/// verification, forgot-password, and the credential checks the web host's
/// Account pages call. Internal to the module — Host (the composition root) may
/// use it directly, but no other module does (kept out of Contracts by design).
///
/// Phased growth (tasks.md): GetSecurityStampAsync (T022) and RegisterAsync (US1)
/// are in; VerifyEmailAsync/ResendVerificationAsync (US2) and
/// RequestPasswordResetAsync/ResetPasswordAsync (US3) extend this same class.
/// </summary>
public sealed class RegistrationService
{
    /// <summary>Platform default (root) organization — self-service sign-ups join it (FR-007).
    /// Must match the root org ID created by ManagementSeeder.</summary>
    public static readonly Guid DefaultOrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly EnrollmentDbContext _context;
    private readonly IUserProvisioning _provisioning;
    private readonly CredentialPolicy _policy;
    private readonly EmailThrottle _throttle;
    private readonly ITransactionalEmailSender _emailSender;
    private readonly PasswordHasher _hasher;

    public RegistrationService(
        EnrollmentDbContext context,
        IUserProvisioning provisioning,
        CredentialPolicy policy,
        EmailThrottle throttle,
        ITransactionalEmailSender emailSender,
        PasswordHasher hasher)
    {
        _context = context;
        _provisioning = provisioning;
        _policy = policy;
        _throttle = throttle;
        _emailSender = emailSender;
        _hasher = hasher;
    }

    // ── Result records (SharedKernel Result<T> conventions, spec 027) ─────────────

    /// <summary>Field-level validation failures for a form (null = no error on that field).</summary>
    public sealed record FieldErrors(string? Name, string? Email, string? Password, string? Confirm);

    /// <summary>Outcome of a sign-up. Succeeded=true means the account was created and
    /// the verification + welcome emails were generated. GeneralError carries
    /// non-field errors (currently: throttling).</summary>
    public sealed record RegistrationResult(bool Succeeded, FieldErrors Errors, string? GeneralError);

    // ── US1: self-service registration ─────────────────────────────────────────────

    /// <summary>
    /// Create a self-service learner account (FR-001..FR-010):
    /// normalize email → throttle → validate (format, strict policy with the specific
    /// failed rule(s), confirmation match) → case-insensitive duplicate check →
    /// create unverified account (Learner, default org, random SecurityStamp) →
    /// set 24 h single-use verification token (stored as SHA-256 hex) → send the
    /// Verification + Welcome emails through the email seam. The password is never
    /// logged (FR-006). <paramref name="baseUrl"/> (e.g. "http://localhost:5000")
    /// builds the absolute link in the verification email.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(
        string name, string email, string password, string confirmPassword, string baseUrl)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedEmail = email?.Trim() ?? string.Empty;
        var normalizedEmail = trimmedEmail.ToLowerInvariant();
        var trimmedConfirm = confirmPassword ?? string.Empty;

        // Throttle per email BEFORE validation (R6): every submission counts.
        if (!_throttle.Allow(normalizedEmail, ThrottleFlow.Signup))
        {
            return new RegistrationResult(false, new FieldErrors(null, null, null, null),
                "Too many sign-up attempts for this email. Please try again later.");
        }

        // Field validation (FR-005, FR-003/FR-004 — specific failed rule(s)).
        var errors = new FieldErrors(null, null, null, null);

        if (string.IsNullOrWhiteSpace(trimmedName))
            errors = errors with { Name = "Full name is required." };

        if (!IsValidEmailFormat(normalizedEmail))
            errors = errors with { Email = "Enter a valid email address." };

        var policyFailures = _policy.Evaluate(password, trimmedName, normalizedEmail);
        if (policyFailures.Count > 0)
            errors = errors with { Password = string.Join(" ", policyFailures) };

        if (password != trimmedConfirm)
            errors = errors with { Confirm = "Passwords do not match." };

        if (errors.Name is not null || errors.Email is not null || errors.Password is not null || errors.Confirm is not null)
            return new RegistrationResult(false, errors, null);

        // Case-insensitive duplicate check (FR-002). The DB unique index is the
        // backstop for concurrent duplicates (handled below).
        if (await _provisioning.ExistsByEmailAsync(normalizedEmail))
            return new RegistrationResult(false, errors with { Email = "Email already in use." }, null);

        // Single-use verification token: 32 random bytes, base64url; only the
        // SHA-256 hex is stored (R4) — a DB leak does not leak working links.
        var token = CreateToken();
        var tokenHash = HashToken(token);

        StudentProvisionedDto student;
        try
        {
            // Self-service: Learner role, default org, UNVERIFIED (FR-007, FR-011).
            student = await _provisioning.CreateAsync(
                trimmedName, normalizedEmail, password, RoleNames.Learner, DefaultOrganizationId, isVerified: false);
        }
        catch (InvalidOperationException)
        {
            // Concurrent duplicate (unique index backstop) or a policy race.
            return new RegistrationResult(false, errors with { Email = "Email already in use." }, null);
        }
        catch (ArgumentException ex)
        {
            // Defensive — the policy was just evaluated; surface it as a field error.
            return new RegistrationResult(false, errors with { Password = ex.Message }, null);
        }

        // Set the pending verification token (same module — direct DbContext write).
        var entity = await _context.Students.FirstAsync(s => s.Id == student.Id);
        entity.VerificationTokenHash = tokenHash;
        entity.VerificationTokenExpiresAt = DateTimeOffset.UtcNow + VerificationTokenLifetime;
        await _context.SaveChangesAsync();

        // FR-008: verification + welcome emails through the seam (never throws, FR-022).
        await _emailSender.SendAsync(new OutboundEmail(
            normalizedEmail,
            EmailPurpose.Verification,
            "Verify your email — Libre LMS",
            BuildVerificationBody(trimmedName, BuildLink(baseUrl, "/Account/Verify?token=" + token))));

        await _emailSender.SendAsync(new OutboundEmail(
            normalizedEmail,
            EmailPurpose.Welcome,
            "Your Libre LMS account has been created",
            BuildWelcomeBody(trimmedName, normalizedEmail, BuildLink(baseUrl, "/Account/Login"))));

        return new RegistrationResult(true, new FieldErrors(null, null, null, null), null);
    }

    // ── US2: email verification (single-use 24 h links) ───────────────────────

    /// <summary>Token state machine (spec 027 FR-012..FR-015, data-model token rules).
    /// Consumed links keep their hash with a null expiry — the hash alone is
    /// unguessable, and this makes AlreadyUsed detectable without a new column.</summary>
    public enum VerificationStatus { Success, Invalid, Expired, AlreadyUsed }

    public sealed record VerifyEmailResult(VerificationStatus Status, string? StudentName);
    public sealed record ResendResult(bool Succeeded, string? Error);

    /// <summary>
    /// Consume a verification link: mark the account verified (FR-012) and consume the
    /// token. Invalid = no account ever held that token; Expired = link older than 24 h;
    /// AlreadyUsed = link was already consumed.
    /// </summary>
    public async Task<VerifyEmailResult> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new VerifyEmailResult(VerificationStatus.Invalid, null);

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.VerificationTokenHash == HashToken(token!));

        if (student is null)
            return new VerifyEmailResult(VerificationStatus.Invalid, null);
        if (student.VerificationTokenExpiresAt is null)
            return new VerifyEmailResult(VerificationStatus.AlreadyUsed, student.Name);
        if (student.VerificationTokenExpiresAt.Value < DateTimeOffset.UtcNow)
            return new VerifyEmailResult(VerificationStatus.Expired, student.Name);

        student.IsEmailVerified = true;
        student.VerificationTokenExpiresAt = null; // consumed (hash kept)
        await _context.SaveChangesAsync();

        return new VerifyEmailResult(VerificationStatus.Success, student.Name);
    }

    /// <summary>
    /// Re-issue the 24 h verification link for a pending (unverified, unconsumed) account
    /// (FR-013). The previous link is invalidated immediately (single-use, FR-016).
    /// Neutral response for unknown/already-verified emails (no account enumeration);
    /// throttled to 3/hour per email.
    /// </summary>
    public async Task<ResendResult> ResendVerificationAsync(string email, string baseUrl)
    {
        var normalizedEmail = (email?.Trim() ?? string.Empty).ToLowerInvariant();

        if (!_throttle.Allow(normalizedEmail, ThrottleFlow.ResendVerification))
            return new ResendResult(false, "Too many verification emails for this email. Please try again later.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Email == normalizedEmail);

        // No account / already verified / no pending token → the same neutral message.
        if (student is null || student.IsEmailVerified || student.VerificationTokenExpiresAt is null)
            return new ResendResult(false, "No pending verification was found for this email.");

        var token = CreateToken();
        student.VerificationTokenHash = HashToken(token);
        student.VerificationTokenExpiresAt = DateTimeOffset.UtcNow + VerificationTokenLifetime;
        await _context.SaveChangesAsync();

        await _emailSender.SendAsync(new OutboundEmail(
            normalizedEmail,
            EmailPurpose.Verification,
            "Verify your email — Libre LMS",
            BuildVerificationBody(student.Name, BuildLink(baseUrl, "/Account/Verify?token=" + token))));

        return new ResendResult(true, null);
    }

    /// <summary>
    /// Credential check for the web host's login (spec 027 US2/US3): neutral
    /// "Invalid email or password." for wrong email AND wrong password (no
    /// enumeration), legacy unsalted-SHA256 hashes upgraded in place (FR-023/024).
    /// </summary>
    public async Task<(bool Success, Guid? StudentId, string? Error)> VerifyCredentialsAsync(string email, string password)
    {
        var normalizedEmail = (email?.Trim() ?? string.Empty).ToLowerInvariant();
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == normalizedEmail);

        if (student is null)
            return (false, null, "Invalid email or password.");

        var (verified, needsUpgrade) = _hasher.Verify(password ?? string.Empty, student.PasswordHash);
        if (!verified)
            return (false, null, "Invalid email or password.");

        // FR-023/024: legacy unsalted-SHA256 seed hashes are upgraded in place on first login.
        if (needsUpgrade)
        {
            student.PasswordHash = _hasher.Hash(password!);
            await _context.SaveChangesAsync();
        }

        return (true, student.Id, null);
    }

    /// <summary>Whether the account exists and its email is verified (login gate, FR-011).</summary>
    public async Task<bool> IsEmailVerifiedAsync(Guid studentId)
    {
        return await _context.Students
            .Where(s => s.Id == studentId)
            .Select(s => s.IsEmailVerified)
            .FirstOrDefaultAsync();
    }

    // ── Foundational: stamp lookup (T022) ──────────────────────────────────────────

    /// <summary>
    /// The account's current SecurityStamp, or null when the account no longer exists.
    /// Used by the host's cookie OnValidatePrincipal to invalidate sessions whose
    /// stamp no longer matches (password reset rotates the stamp — FR-017).
    /// </summary>
    public async Task<Guid?> GetSecurityStampAsync(Guid studentId)
    {
        return await _context.Students
            .Where(s => s.Id == studentId)
            .Select(s => s.SecurityStamp)
            .FirstOrDefaultAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    /// <summary>32 random bytes, base64url (no padding) — the token value in the link.</summary>
    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>SHA-256 hex of the token value — the only form stored on the account (R4).</summary>
    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Absolute URL from the current request's scheme/host (dev: http://host/...).</summary>
    private static string BuildLink(string baseUrl, string pathAndQuery) =>
        baseUrl.TrimEnd('/') + pathAndQuery;

    /// <summary>Verification email body (contracts/email-messages.md §1).</summary>
    private static string BuildVerificationBody(string name, string absoluteLink) =>
        $$"""
        Hi {{name}},

        welcome to Libre LMS. Please verify your email address by opening this link:

        {{absoluteLink}}

        This link expires in 24 hours and works once. If you did not create an account, you can ignore this email.
        """;

    /// <summary>Welcome email body (contracts/email-messages.md §2). No password ever appears.</summary>
    private static string BuildWelcomeBody(string name, string email, string loginUrl) =>
        $$"""
        Hi {{name}},

        your Libre LMS account for {{email}} has been created.

        After you verify your email, you can sign in at {{loginUrl}}.

        — The Libre LMS team
        """;

    /// <summary>Minimal well-formedness check: exactly one '@', non-empty local part and
    /// domain, domain contains a dot. (Full RFC validation is out of scope for a dev-scale LMS.)</summary>
    private static bool IsValidEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var atIndex = email.LastIndexOf('@');
        if (atIndex <= 0 || atIndex != email.IndexOf('@'))
            return false;

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        return local.Length > 0 && domain.Length > 0 && domain.Contains('.');
    }
}
