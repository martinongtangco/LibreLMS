using Microsoft.EntityFrameworkCore;
using LibreLms.Contracts.Enrollment;
using LibreLms.Modules.Enrollment.Domain;
using LibreLms.Modules.Enrollment.Infrastructure;

namespace LibreLms.Modules.Enrollment.Application;

/// <summary>
/// Cross-module account creation and maintenance over EnrollmentDbContext, applying the
/// shared credential core (spec 027): strict password policy, PBKDF2 hashing, normalized
/// unique email, random SecurityStamp. Expected failures throw (ArgumentException /
/// InvalidOperationException / KeyNotFoundException) — callers map them to HTTP.
/// </summary>
public sealed class UserProvisioningService : IUserProvisioning
{
    private readonly EnrollmentDbContext _context;
    private readonly PasswordHasher _hasher;
    private readonly CredentialPolicy _policy;

    public UserProvisioningService(EnrollmentDbContext context, PasswordHasher hasher, CredentialPolicy policy)
    {
        _context = context;
        _hasher = hasher;
        _policy = policy;
    }

    public async Task<StudentProvisionedDto> CreateAsync(
        string name, string email, string password, string role, Guid organizationId, bool isVerified)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!IsValidEmailFormat(normalizedEmail))
            throw new ArgumentException("A valid email address is required.");

        var failures = _policy.Evaluate(password, name, normalizedEmail);
        if (failures.Count > 0)
            throw new ArgumentException(string.Join(" ", failures));

        if (await _context.Students.AnyAsync(s => s.Email == normalizedEmail))
            throw new InvalidOperationException($"A user with email '{email}' already exists.");

        var student = new Student
        {
            Name = name.Trim(),
            Email = normalizedEmail,
            PasswordHash = _hasher.Hash(password),
            Roles = role,
            OrganizationId = organizationId,
            IsEmailVerified = isVerified,
            SecurityStamp = Guid.NewGuid()
        };

        _context.Students.Add(student);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique email index is the backstop for concurrent duplicate sign-ups.
            throw new InvalidOperationException($"A user with email '{email}' already exists.");
        }

        return ToDto(student);
    }

    public async Task<StudentProvisionedDto?> GetByIdAsync(Guid studentId)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        return student is null ? null : ToDto(student);
    }

    public async Task<IList<StudentProvisionedDto>> ListByOrgAsync(Guid orgId, string? roleFilter = null)
    {
        var query = _context.Students.Where(s => s.OrganizationId == orgId);
        if (!string.IsNullOrEmpty(roleFilter))
            query = query.Where(s => s.Roles == roleFilter);

        var students = await query.ToListAsync();
        return students.Select(ToDto).ToList();
    }

    public async Task<IList<StudentProvisionedDto>> ListAsync(string? roleFilter = null)
    {
        var query = _context.Students.AsQueryable();
        if (!string.IsNullOrEmpty(roleFilter))
            query = query.Where(s => s.Roles == roleFilter);

        var students = await query.ToListAsync();
        return students.Select(ToDto).ToList();
    }

    public async Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role, Guid? organizationId)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(name))
            student.Name = name.Trim();

        if (!string.IsNullOrWhiteSpace(role))
            student.Roles = role;

        if (organizationId.HasValue)
            student.OrganizationId = organizationId.Value;

        await _context.SaveChangesAsync();
        return ToDto(student);
    }

    public async Task DeleteAsync(Guid studentId)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student is null)
            throw new KeyNotFoundException("User not found.");

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return await _context.Students.AnyAsync(s => s.Email == normalized);
    }

    /// <summary>Minimal well-formedness check: exactly one '@', non-empty local part and domain,
    /// domain contains a dot. (Full RFC validation is out of scope for a dev-scale LMS.)</summary>
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

    private static StudentProvisionedDto ToDto(Student s) =>
        new(s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, s.CreatedAt, s.IsEmailVerified);
}
