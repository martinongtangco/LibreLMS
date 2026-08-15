# Module Contracts: Formal Signup & Registration

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-15

C# interface contracts at the compiled module boundaries (Constitution Principle III).
Signatures only — implementation detail lives in the implementation phase. All new
contract types are DTO- and interface-only (no module internals leak).

Namespaces: `LibreLms.SharedKernel` (the shared kernel project — same home as
`Entity<T>`/`RoleNames`/`IDomainEvent`), `LibreLms.Contracts.Enrollment`,
`LibreLms.Contracts.Catalog`.

---

## SharedKernel (new)

### ITransactionalEmailSender

```csharp
/// <summary>Seam for sending transactional email without knowing the provider.</summary>
public interface ITransactionalEmailSender
{
    Task SendAsync(OutboundEmail email);
}

public enum EmailPurpose { Verification, Welcome, PasswordReset }

public record OutboundEmail(string To, EmailPurpose Purpose, string Subject, string Body);
```

Implementations: `MockEmailSender` (this slice, Host — records to the dev outbox + logs,
sends nothing real). Future: a SendGrid-based implementation swapped in via DI
(ADR-0004).

---

## Enrollment.Contracts (new)

### IUserProvisioning

```csharp
/// <summary>Create and maintain platform users (accounts) from other modules.</summary>
public interface IUserProvisioning
{
    /// <summary>Create an account. Enforces the strict password policy and email
    /// uniqueness (case-insensitive). Throws/Result on duplicate email or policy
    /// failure. <paramref name="isVerified"/>: admin-created = true; self-service = false.</summary>
    Task<StudentProvisionedDto> CreateAsync(string name, string email, string password,
        string role, Guid organizationId, bool isVerified);

    Task<StudentProvisionedDto?> GetByIdAsync(Guid studentId);
    Task<IList<StudentProvisionedDto>> ListByOrgAsync(Guid orgId, string? roleFilter = null);
    Task<StudentProvisionedDto> UpdateAsync(Guid studentId, string? name, string? role,
        Guid? organizationId);
    Task DeleteAsync(Guid studentId);
    Task<bool> ExistsByEmailAsync(string email);
}

public record StudentProvisionedDto(Guid Id, string Name, string Email, string Role,
    Guid OrganizationId, DateTimeOffset CreatedAt, bool IsEmailVerified);
```

Implementation: new `UserProvisioningService` in Enrollment/Application (wraps
`EnrollmentDbContext` + the shared credential core). Replaces the direct-DbContext usage
in Management's `UserService` and the SuperUser seeding in `ManagementSeeder`.

### IUserLookup

```csharp
/// <summary>Read-only user facts other modules need (no account mutation).</summary>
public interface IUserLookup
{
    Task<UserScopeInfo?> GetUserScopeAsync(Guid studentId);      // role + org
    Task<int> CountLearnersAsync(Guid? organizationId = null);
    Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync();
}

public record UserScopeInfo(Guid OrganizationId, string Role);
public record OrgLearnerCount(Guid OrganizationId, int Count);
```

Implementation: `UserLookupService` (Enrollment). Covers today's `UserInfoLookup`,
`OrganizationService` user counts, and `DashboardService` learner counts.
(`UserScopeInfo` mirrors the existing `Management.Contracts.UserScopeInfo` shape; the
Management-side `IUserInfoLookup` implementation delegates here.)

### IEnrollmentAdmin

```csharp
/// <summary>Admin operations on enrollments, with existence checks, for other modules.</summary>
public interface IEnrollmentAdmin
{
    Task<AdminEnrollResult> EnrollAsync(Guid studentId, Guid courseId);
    Task<IList<AdminEnrollResult>> EnrollManyAsync(Guid courseId, IEnumerable<Guid> studentIds);
    Task<bool> UnenrollAsync(Guid enrollmentId);
    Task<IList<AdminEnrollmentInfo>> GetStudentEnrollmentsAsync(Guid studentId);
    Task<int> CountEnrollmentsAsync(Guid? organizationId = null);
    Task<IList<RecentEnrollmentInfo>> GetRecentEnrollmentsAsync(int take);
}

public record AdminEnrollResult(Guid EnrollmentId, Guid StudentId, Guid CourseId,
    bool AlreadyEnrolled);
public record AdminEnrollmentInfo(Guid EnrollmentId, Guid StudentId, Guid CourseId,
    DateTimeOffset EnrolledAt, string CourseTitle);
public record RecentEnrollmentInfo(Guid EnrollmentId, Guid StudentId, string StudentName,
    string StudentEmail, Guid CourseId, string CourseTitle, DateTimeOffset EnrolledAt);
```

Implementation: `EnrollmentAdminService` (Enrollment) — uses `ICourseLookup` internally
for titles/existence. Covers today's `AdminEnrollmentService` and
`DashboardService` recent-enrollments queries.

---

## Catalog.Contracts (extended / new)

### ICourseLookup (extended)

```csharp
public interface ICourseLookup
{
    Task<CourseSummary?> GetCourseAsync(Guid courseId);          // existing
    Task<int> CountAsync();                                      // NEW
    Task<int> CountByOrgAsync(Guid organizationId);              // NEW
    Task<IList<CourseSummary>> GetCoursesAsync(IEnumerable<Guid> courseIds); // NEW (batch)
}
```

### ICourseAdmin (new)

```csharp
/// <summary>Catalog mutations exposed across the module boundary.</summary>
public interface ICourseAdmin
{
    Task<bool> DeleteAsync(Guid courseId);   // used by CourseVisibilityService
}
```

Implementation: existing Catalog application services (extend, don't wrap —
Constitution II).

---

## RegistrationService (Enrollment.Application — internal, NOT a cross-module contract)

Self-service lifecycle used by Host pages (Host may reference module internals as the
composition root). Kept out of Contracts because no *other module* calls it:

```csharp
public class RegistrationService
{
    Task<RegistrationResult> RegisterAsync(string name, string email, string password);
    Task<VerifyResult> VerifyEmailAsync(string token);
    Task<ResendResult> ResendVerificationAsync(string email);
    Task<ResetRequestResult> RequestPasswordResetAsync(string email);
    Task<ResetResult> ResetPasswordAsync(string token, string newPassword);
    Task<LoginCheckResult> VerifyCredentialsAsync(string email, string password);
        // returns: credentials ok? + isVerified + (legacy hash upgraded?)
    Task<Guid?> GetSecurityStampAsync(Guid studentId);   // for cookie re-validation
}
```

Result records follow SharedKernel's `Result<T>` conventions. All of these enforce the
shared credential core (`CredentialPolicy` + `PasswordHasher`) and `EmailThrottle`, and
send mail only through `ITransactionalEmailSender`.
