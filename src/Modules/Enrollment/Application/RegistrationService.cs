using Microsoft.EntityFrameworkCore;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Self-service account lifecycle for Student (spec 027): registration, email
/// verification, forgot-password, and the credential checks the web host's
/// Account pages call. Internal to the module — Host (the composition root) may
/// use it directly, but no other module does (kept out of Contracts by design).
///
/// Phased growth (tasks.md): GetSecurityStampAsync lands with the foundational
/// stamp re-validation (T022); RegisterAsync (US1), VerifyEmailAsync/
/// ResendVerificationAsync (US2), and RequestPasswordResetAsync/ResetPasswordAsync
/// (US3) extend this same class.
/// </summary>
public sealed class RegistrationService
{
    private readonly EnrollmentDbContext _context;

    public RegistrationService(EnrollmentDbContext context)
    {
        _context = context;
    }

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
}
