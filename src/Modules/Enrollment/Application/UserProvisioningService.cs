using System.Data;
using Microsoft.Data.SqlClient;
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

    /// <summary>Paged admin listing over the AdminListLearners stored procedure (spec 032):
    /// case-insensitive contains search on name OR email, exact role match, name-ascending.
    /// The procedure's result set deliberately excludes credential columns
    /// (PasswordHash/SecurityStamp are never selected or returned).</summary>
    public async Task<StudentPageResult> ListPagedAsync(string? search, string? roleFilter, int pageNumber, int pageSize)
    {
        // Trim whitespace from search term
        search = search?.Trim();
        if (string.IsNullOrWhiteSpace(search))
            search = null;

        if (string.IsNullOrWhiteSpace(roleFilter))
            roleFilter = null;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        try
        {
            using var command = new SqlCommand("AdminListLearners", (SqlConnection)connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = search ?? (object)DBNull.Value;
            command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = roleFilter ?? (object)DBNull.Value;
            command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
            command.Parameters.Add("@PageNumber", SqlDbType.Int).Value = pageNumber;

            var items = new List<StudentProvisionedDto>();
            var totalCount = 0;

            using var reader = await command.ExecuteReaderAsync();

            // Result Set 1: learner rows (columns 0..8 map 1:1 onto StudentProvisionedDto;
            // column 8 is ThemePreference — spec 042)
            while (reader.Read())
            {
                items.Add(new StudentProvisionedDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetGuid(4),
                    reader.GetDateTimeOffset(5),
                    reader.GetBoolean(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    NormalizeTheme(reader.IsDBNull(8) ? null : reader.GetString(8))
                ));
            }

            // Move to Result Set 2: Total count
            await reader.NextResultAsync();
            if (reader.Read())
            {
                totalCount = reader.GetInt32(0);
            }

            return new StudentPageResult(items, totalCount);
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    public async Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role, Guid? organizationId,
        string? avatarPath = null)
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

        // Spec 030: null/empty = no change; the profile photo save is the only writer.
        if (!string.IsNullOrWhiteSpace(avatarPath))
            student.AvatarPath = avatarPath;

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
        new(s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, s.CreatedAt, s.IsEmailVerified, s.AvatarPath,
            NormalizeTheme(s.ThemePreference));

    /// <summary>Normalize a stored theme preference to the spec 042 value set
    /// ("System"/"Light"/"Dark"); anything else falls back to "System".</summary>
    private static string NormalizeTheme(string? value) =>
        value is null ? "System" : value switch { "System" or "Light" or "Dark" => value, _ => "System" };
}
