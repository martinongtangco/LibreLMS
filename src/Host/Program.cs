using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using LibreLms.Modules.Catalog;
using LibreLms.Modules.Catalog.Infrastructure;
using LibreLms.Modules.Catalog.Application;
using LibreLms.Modules.Catalog.Endpoints;
using LibreLms.Modules.Enrollment;
using LibreLms.Modules.Enrollment.Infrastructure;
using LibreLms.Modules.Enrollment.Application;
using LibreLms.Modules.Enrollment.Endpoints;
using LibreLms.Modules.Scorm;
using LibreLms.Modules.Scorm.Application;
using LibreLms.Modules.Scorm.Infrastructure;
using LibreLms.Modules.Scorm.Endpoints;
using LibreLms.Modules.Management;
using LibreLms.Modules.Management.Infrastructure;
using LibreLms.Modules.Management.Endpoints;
using LibreLms.Modules.Management.Application;
using LibreLms.Host.ManagementAuth;
using LibreLms.SharedKernel;
using static LibreLms.Host.ScormHelpers;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core contexts with MSSQL
void ConfigureDbContext(DbContextOptionsBuilder opts, string? connStr)
{
    opts.UseSqlServer(connStr, sql => sql.MigrationsAssembly(typeof(Program).Assembly));
    opts.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
}

builder.Services.AddDbContext<CatalogDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("Sql")));
builder.Services.AddDbContext<EnrollmentDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("Sql")));

// Register module services
builder.Services.AddCatalogModule();
builder.Services.AddEnrollmentModule();
builder.Services.AddScormModule();
builder.Services.AddManagementModule();

// Spec 027: transactional email seam (mock implementation + developer outbox).
// The outbox is an in-memory dev artifact (Constitution VI); the sender never throws.
builder.Services.AddSingleton<LibreLms.Host.Mail.DevEmailOutbox>();
builder.Services.AddSingleton<LibreLms.SharedKernel.ITransactionalEmailSender, LibreLms.Host.Mail.MockEmailSender>();

// Spec 027: self-service account lifecycle (used by Host's Account pages and the
// cookie OnValidatePrincipal stamp re-check). Host is the composition root, so it
// may register module-internal services directly (Constitution I/III).
builder.Services.AddSingleton<LibreLms.Modules.Enrollment.Application.EmailThrottle>();
builder.Services.AddScoped<LibreLms.Modules.Enrollment.Application.RegistrationService>();

// Spec 030: re-issue the auth cookie from a fresh Student row after a profile change
// (name/photo save) so the nav shows the update immediately (R2 "RefreshSignIn").
builder.Services.AddScoped<LibreLms.Host.ManagementAuth.AuthCookieRefresher>();

// Register EF Core context for Scorm
builder.Services.AddDbContext<ScormDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("Sql")));

// Register EF Core context for Management
builder.Services.AddDbContext<ManagementDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("Sql")));

// Configure Scorm module with wwwRoot path
var wwwRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwRootPath);
builder.Services.ConfigureScormModule(wwwRootPath);

// Register Valkey (StackExchange.Redis) for SCORM session storage
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(
        builder.Configuration.GetConnectionString("Valkey") ?? "localhost:6379",
        true);
    config.AbortOnConnectFail = false; // Graceful degradation
    return ConnectionMultiplexer.Connect(config);
});

// Authentication (Cookie-based for web portal)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookie";
    options.DefaultChallengeScheme = "Cookie";
})
.AddCookie("Cookie", options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";

    // Spec 027 FR-017 / ADR-0006: re-validate the account's SecurityStamp on every
    // authenticated request. A password reset rotates the stamp, so all pre-existing
    // sessions are invalidated at their next request. One indexed primary-key lookup
    // per request — trivial at dev scale; a TTL cache was rejected because it would
    // delay reset invalidation by up to the TTL (see ADR-0006).
    options.Events.OnValidatePrincipal = async context =>
    {
        var principal = context.Principal;
        var stampClaim = principal.FindFirst(LibreLms.Host.ManagementAuth.SecurityClaims.SecurityStamp)?.Value;
        var idClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Missing stamp claim (cookie issued before spec 027) or missing identity
        // -> treat as anonymous; the user signs in once (documented, accepted).
        if (string.IsNullOrEmpty(stampClaim) || !Guid.TryParse(idClaim, out var studentId))
        {
            context.RejectPrincipal();
            return;
        }

        var registration = context.HttpContext.RequestServices.GetRequiredService<LibreLms.Modules.Enrollment.Application.RegistrationService>();
        var currentStamp = await registration.GetSecurityStampAsync(studentId);
        if (currentStamp is null ||
            !string.Equals(currentStamp.Value.ToString("D"), stampClaim, StringComparison.OrdinalIgnoreCase))
        {
            context.RejectPrincipal();
        }
    };
});

// Authorization: Register org-scope handler and policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperUserOnly", policy =>
        policy.RequireRole(RoleNames.SuperUser));
    options.AddPolicy("OrgAdminOrSuperUser", policy =>
        policy.RequireRole(RoleNames.SuperUser, RoleNames.OrgAdmin));
    options.AddPolicy("AuthenticatedWithOrgScope", policy =>
        policy.RequireAssertion(context =>
        {
            var role = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.Role);
            return role == RoleNames.SuperUser || role == RoleNames.OrgAdmin;
        }));
});
builder.Services.AddScoped<IAuthorizationHandler, OrgScopeAuthorizationHandler>();

// Add Razor Pages and HttpClient
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();

// Ensure database and tables exist, then seed data on startup
using (var scope = app.Services.CreateScope())
{
    var catalogCtx = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var enrollmentCtx = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
    var scormCtx = scope.ServiceProvider.GetRequiredService<ScormDbContext>();
    var managementCtx = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
    var hostEnv = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

    // Apply all migrations (creates database if it doesn't exist)
    catalogCtx.Database.Migrate();
    enrollmentCtx.Database.Migrate();
    scormCtx.Database.Migrate();
    managementCtx.Database.Migrate();
    enrollmentCtx.Database.Migrate();
    scormCtx.Database.Migrate();
    managementCtx.Database.Migrate();

    // Seed organizations (root org only — the seeded SuperUser Student moved to
    // EnrollmentSeeder in spec 027, so Management no longer touches Enrollment).
    if (!managementCtx.Organizations.Any())
    {
        LibreLms.Modules.Management.Infrastructure.ManagementSeeder.Seed(managementCtx);
    }

    // Seed catalog
    if (!catalogCtx.Courses.Any())
    {
        LibreLms.Modules.Catalog.Infrastructure.CatalogSeeder.Seed(catalogCtx);
    }

    // Seed students (check for a specific seeded email, not just Any(),
    // since ManagementSeeder also creates a Student in this context)
    if (!enrollmentCtx.Students.Any(s => s.Email == "alice@example.com"))
    {
        LibreLms.Modules.Enrollment.Infrastructure.EnrollmentSeeder.Seed(enrollmentCtx);
    }

    // Seed Scorm sample package
    await ScormSeeder.SeedAsync(scormCtx, hostEnv.WebRootPath);
}

// Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

// === Catalog Module Endpoints ===
var courses = app.MapGroup("/api/courses");
courses.MapGet("/", async (CourseCatalogService service, string? search, string? category) =>
{
    var courseList = await service.ListAsync(search, category);
    var dto = courseList.Select(c => new CourseDto(c.Id, c.Title, c.ShortDescription, c.Category, c.Duration));
    return Results.Ok(new { courses = dto });
});

// POST /api/courses — Admin-only course creation
courses.MapPost("/", [Authorize(Roles = "SuperUser,OrgAdmin")] async (CourseCatalogService service, [FromBody] LibreLms.Modules.Catalog.Endpoints.CreateCourseRequest request, HttpContext httpContext) =>
{
    // Use authenticated user's OrganizationId when the request doesn't provide one
    if (!request.OrganizationId.HasValue)
    {
        var orgIdStr = httpContext.User.FindFirst(LibreLms.Host.ManagementAuth.OrgClaimTypes.OrganizationId)?.Value;
        if (Guid.TryParse(orgIdStr, out var parsedOrgId))
        {
            request = request with { OrganizationId = parsedOrgId };
        }
    }
    var course = await service.CreateAsync(request);
    return Results.Created($"/api/courses/{course.Id}", new CourseDto(course.Id, course.Title, course.ShortDescription, course.Category, course.Duration));
});

courses.MapGet("/{id:guid}", async (CourseCatalogService service, ScormPackageService scormService, Guid id) =>
{
    var course = await service.GetByIdAsync(id);
    if (course is null)
        return Results.NotFound();

    var scormPackage = await scormService.GetPackageByCourseIdAsync(id);

    return Results.Ok(new
    {
        course.Id,
        course.Title,
        course.ShortDescription,
        course.FullDescription,
        course.Category,
        course.Duration,
        IsScorm = scormPackage is not null,
        ScormPackageId = scormPackage?.Id
    });
});

// === Enrollment Module Endpoints ===
var enrollments = app.MapGroup("/api/enrollments");
enrollments.MapPost("/", [Authorize] async (
    EnrollmentService service,
    [FromBody] EnrollRequest request,
    HttpContext httpContext) =>
{
    var studentId = GetStudentId(httpContext);
    var (enrollment, isDuplicate, courseNotFound) = await service.EnrollAsync(studentId, request.CourseId);

    if (courseNotFound)
        return Results.BadRequest(new { error = "Course not found" });

    if (isDuplicate)
        return Results.Conflict(new { error = "Already enrolled in this course" });

    return Results.Created($"/api/enrollments/{enrollment.Id}", new LibreLms.Modules.Enrollment.Endpoints.EnrollmentDto(
        enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.EnrolledAt));
});

enrollments.MapGet("/my", [Authorize] async (
    EnrollmentService service,
    HttpContext httpContext) =>
{
    var studentId = GetStudentId(httpContext);
    var enrollmentsList = await service.GetMyEnrollmentsAsync(studentId);

    var result = enrollmentsList.Select(e => new MyEnrollmentDto(
        e.Enrollment.Id,
        e.Enrollment.CourseId,
        e.CourseTitle,
        e.Enrollment.EnrolledAt));

    return Results.Ok(new { enrollments = result });
});

// === Scorm Module Endpoints ===
var scorm = app.MapGroup("/api/scorm").WithTags("Scorm");

// POST /api/scorm/{courseId}/launch
scorm.MapPost("/{courseId:guid}/launch", [Authorize] async (
    ScormSessionService sessionService,
    HttpContext httpContext,
    Guid courseId) =>
{
    var studentId = GetStudentId(httpContext);
    var result = await sessionService.LaunchAsync(studentId, courseId);

    if (result.Error == "Student is not enrolled in this course.")
        return Results.Forbid();

    if (!result.Success)
        return Results.BadRequest(new { error = result.Error });

    return Results.Ok(new
    {
        sessionId = result.SessionId,
        contentUrl = result.ContentUrl,
        entry = result.EntryMode,
        attemptNumber = result.AttemptNumber
    });
});

// POST /api/scorm/upload
scorm.MapPost("/upload", [Authorize(Roles = "SuperUser,OrgAdmin")] async (
    ScormPackageService packageService,
    HttpContext httpContext,
    IFormCollection form) =>
{

    var file = form.Files.GetFile("package");
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "File must be a ZIP archive" });

    var courseId = Guid.Empty;
    if (form.ContainsKey("courseId") && Guid.TryParse(form["courseId"], out var parsedCourseId))
        courseId = parsedCourseId;

    if (courseId == Guid.Empty)
        return Results.BadRequest(new { error = "courseId is required" });

    using var stream = file.OpenReadStream();
    var (package, error) = await packageService.UploadAsync(stream, courseId);

    if (error is not null)
        return Results.BadRequest(new { error });

    return Results.Created($"/api/scorm/packages/{package.Id}", new
    {
        packageId = package.Id,
        courseId = package.CourseId,
        title = package.ManifestTitle,
        launchPath = package.LaunchPath
    });
});

// GET /api/scorm/attempts/my
scorm.MapGet("/attempts/my", [Authorize] async (
    ScormAttemptService attemptService,
    HttpContext httpContext) =>
{
    var studentId = GetStudentId(httpContext);
    var attempts = await attemptService.GetMyAttemptsAsync(studentId);

    var result = attempts.Select(a => new
    {
        a.Id,
        a.CourseId,
        a.CourseTitle,
        a.AttemptNumber,
        a.Status,
        a.ScoreRaw,
        a.SessionTime,
        a.StartedAt,
        a.CompletedAt,
        a.LastCommitAt
    });

    return Results.Ok(new { attempts = result });
});

// === Scorm Session Endpoints ===
var sessionGroup = app.MapGroup("/api/scorm/session/{sessionId:guid}").WithTags("Scorm Session");

sessionGroup.MapPost("/setValue", async (
    ScormSessionService sessionService,
    [FromBody] SetValueRequest request,
    Guid sessionId) =>
{
    var result = await sessionService.SetValueAsync(sessionId, request.Element, request.Value);
    if (!result.Success)
        return Results.BadRequest(new { success = false, errorCode = result.ErrorCode, errorMsg = result.ErrorMsg });
    return Results.Ok(new { success = true });
});

sessionGroup.MapGet("/getValue", async (
    ScormSessionService sessionService,
    Guid sessionId,
    [FromQuery] string element) =>
{
    var result = await sessionService.GetValueAsync(sessionId, element);
    if (!result.Found)
        return Results.NotFound();
    return Results.Ok(new { value = result.Value });
});

sessionGroup.MapPost("/commit", async (
    ScormSessionService sessionService,
    Guid sessionId) =>
{
    var result = await sessionService.CommitAsync(sessionId);
    if (!result.Success)
        return Results.NotFound(new { error = result.Error });
    return Results.Ok(new { success = true, committedAt = result.CommittedAt });
});

sessionGroup.MapPost("/finish", async (
    ScormSessionService sessionService,
    [FromBody] FinishRequest? request,
    Guid sessionId) =>
{
    var result = await sessionService.FinishAsync(sessionId, request?.Exit ?? "normal");
    if (!result.Success)
        return Results.NotFound(new { error = result.Error });
    return Results.Ok(new { success = true, status = result.Status, score = result.Score });
});

// SCORM API JavaScript shim
app.MapGet("/api/scorm/session/{sessionId:guid}/api.js", (Guid sessionId) =>
    Results.Text(ScormApiScriptContent, "application/javascript"))
.DisableAntiforgery();

// === Management Module Endpoints ===

// User Management Endpoints
var users = app.MapGroup("/api/users")
    .WithTags("Users")
    .RequireAuthorization();

users.MapGet("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.UserService service,
    [Microsoft.AspNetCore.Mvc.FromQuery] Guid? organizationId,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? role) =>
{
    // For simplicity, SuperUser sees all; OrgAdmin would need subtree filtering
    var usersList = await service.ListAllAsync(role);
    return Results.Ok(new { users = usersList });
});

users.MapGet("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.UserService service, Guid id) =>
{
    var user = await service.GetByIdAsync(id);
    if (user is null) return Results.NotFound();
    return Results.Ok(user);
});

users.MapPost("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.UserService service,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Host.ManagementDtos.CreateUserRequest request) =>
{
    try
    {
        var student = await service.CreateAsync(request.Name, request.Email, request.Password, request.Role, request.OrganizationId);
        return Results.Created($"/api/users/{student.Id}", new LibreLms.Host.ManagementDtos.UserCreatedDto(student.Id, student.Name, student.Email, student.Role, student.OrganizationId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

users.MapPut("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.UserService service, Guid id,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Host.ManagementDtos.UpdateUserRequest request) =>
{
    try
    {
        var student = await service.UpdateAsync(id, request.Name, request.Role, request.OrganizationId);
        return Results.Ok(new LibreLms.Host.ManagementDtos.UserUpdatedDto(student.Id, student.Name, student.Email, student.Role, student.OrganizationId));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

users.MapDelete("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.UserService service, Guid id) =>
{
    try
    {
        await service.DeleteAsync(id);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Organization Management Endpoints
var orgs = app.MapGroup("/api/organizations")
    .WithTags("Organizations")
    .RequireAuthorization();

// GET /api/organizations — list all orgs
orgs.MapGet("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service,
    [Microsoft.AspNetCore.Mvc.FromQuery] Guid? parentId) =>
{
    var list = parentId.HasValue
        ? await service.ListByParentAsync(parentId.Value)
        : await service.ListAllAsync();
    var dto = list.Select(o => new LibreLms.Modules.Management.Endpoints.OrganizationDto(o.Id, o.Name, o.Description, o.ParentId, o.CreatedAt));
    return Results.Ok(new { organizations = dto });
});

// GET /api/organizations/picker — for dropdown selection
orgs.MapGet("/picker", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service) =>
{
    var list = await service.ListAllAsync();
    var dto = list.Select(o => new LibreLms.Modules.Management.Endpoints.OrganizationPickerDto(o.Id, o.Name));
    return Results.Ok(new { organizations = dto });
});

// GET /api/organizations/{id} — get single org
orgs.MapGet("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service, Guid id) =>
{
    var org = await service.GetByIdAsync(id);
    if (org is null) return Results.NotFound();
    return Results.Ok(new
    {
        org.Id, org.Name, org.Description, org.ParentId, org.CreatedAt,
        Children = org.Children.Select(c => new LibreLms.Modules.Management.Endpoints.OrganizationPickerDto(c.Id, c.Name))
    });
});

// POST /api/organizations — create org
orgs.MapPost("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Modules.Management.Endpoints.CreateOrganizationRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Organization name is required." });
    try
    {
        var org = await service.CreateAsync(request.Name, request.Description, request.ParentId);
        return Results.Created($"/api/organizations/{org.Id}", new LibreLms.Modules.Management.Endpoints.OrganizationDto(org.Id, org.Name, org.Description, org.ParentId, org.CreatedAt));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// PUT /api/organizations/{id} — update org
orgs.MapPut("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service, Guid id,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Modules.Management.Endpoints.UpdateOrganizationRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Organization name is required." });
    try
    {
        var org = await service.UpdateAsync(id, request.Name, request.Description);
        return Results.Ok(new LibreLms.Modules.Management.Endpoints.OrganizationDto(org.Id, org.Name, org.Description, org.ParentId, org.CreatedAt));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// DELETE /api/organizations/{id} — soft delete org
orgs.MapDelete("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.OrganizationService service, Guid id) =>
{
    var (canDelete, reason) = await service.CanDeleteAsync(id);
    if (!canDelete)
        return Results.BadRequest(new { error = reason });
    try
    {
        await service.DeleteAsync(id);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

// === Admin Course Management Endpoints ===
var adminCourses = app.MapGroup("/api/admin/courses")
    .WithTags("Admin Courses")
    .RequireAuthorization();

adminCourses.MapGet("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.CourseVisibilityService service,
    [Microsoft.AspNetCore.Mvc.FromQuery] Guid? organizationId) =>
{
    var courses = organizationId.HasValue
        ? await service.GetVisibleCoursesAsync(organizationId.Value)
        : await service.GetAllCoursesAsync();
    return Results.Ok(new { courses });
});

adminCourses.MapPut("/{id:guid}/visibility", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.CourseVisibilityService service,
    Guid id,
    [Microsoft.AspNetCore.Mvc.FromQuery] Guid organizationId,
    [Microsoft.AspNetCore.Mvc.FromQuery] bool isHidden,
    [Microsoft.AspNetCore.Mvc.FromQuery] Guid? createdBy) =>
{
    try
    {
        var @override = await service.SetVisibilityOverrideAsync(organizationId, id, isHidden, createdBy);
        return Results.Ok(new { id = @override.Id, courseId = @override.CourseId, organizationId = @override.OrganizationId, isHidden = @override.IsHidden });
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

adminCourses.MapDelete("/{id:guid}", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.CourseVisibilityService service, Guid id) =>
{
    try { await service.DeleteCourseAsync(id); return Results.NoContent(); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});

// === Admin Dashboard Endpoints ===
var dashboard = app.MapGroup("/api/dashboard")
    .WithTags("Dashboard")
    .RequireAuthorization();

dashboard.MapGet("/", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin,Learner")] async (
    LibreLms.Modules.Management.Application.DashboardService service,
    System.Security.Claims.ClaimsPrincipal user) =>
{
    var role = user.FindFirstValue(System.Security.Claims.ClaimTypes.Role);
    var studentIdStr = user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(studentIdStr, out var studentId)) return Results.Unauthorized();

    if (role == RoleNames.SuperUser)
    {
        var metrics = await service.GetSystemMetricsAsync();
        return Results.Ok(new { role = "SuperUser", metrics = metrics });
    }
    if (role == RoleNames.OrgAdmin)
    {
        var orgIdStr = user.FindFirstValue(OrgClaimTypes.OrganizationId);
        if (!Guid.TryParse(orgIdStr, out var orgId)) return Results.Unauthorized();
        var metrics = await service.GetOrgMetricsAsync(orgId);
        return Results.Ok(new { role = "OrgAdmin", metrics = metrics });
    }
    if (role == RoleNames.Learner)
    {
        var metrics = await service.GetPersonalMetricsAsync(studentId);
        return Results.Ok(new { role = "Learner", metrics = metrics });
    }
    return Results.Unauthorized();
});

dashboard.MapGet("/activity", [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperUser,OrgAdmin")] async (
    LibreLms.Modules.Management.Application.DashboardService service,
    [Microsoft.AspNetCore.Mvc.FromQuery] int limit = 10) =>
{
    var activities = await service.GetRecentActivityAsync(limit);
    return Results.Ok(new { activities });
});

// === Admin Enrollment Endpoints ===
var adminEnrollments = app.MapGroup("/api/admin/enrollments")
    .WithTags("Admin Enrollments")
    .RequireAuthorization(new AuthorizeAttribute { Roles = "SuperUser,OrgAdmin" });

adminEnrollments.MapGet("/", async (
    LibreLms.Modules.Management.Application.AdminEnrollmentService service,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? student,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? course) =>
{
    var enrollments = await service.ListAllEnrollmentsAsync(student, course);
    return Results.Ok(new { enrollments });
});

adminEnrollments.MapPost("/", async (
    LibreLms.Modules.Management.Application.AdminEnrollmentService service,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Modules.Management.Endpoints.CreateEnrollmentRequest request) =>
{
    try
    {
        var enrollment = await service.EnrollAsync(request.StudentId, request.CourseId);
        return Results.Created($"/api/admin/enrollments/{enrollment.EnrollmentId}", new { enrollment.EnrollmentId, enrollment.StudentId, enrollment.CourseId, enrollment.EnrolledAt });
    }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

adminEnrollments.MapPost("/bulk", async (
    LibreLms.Modules.Management.Application.AdminEnrollmentService service,
    [Microsoft.AspNetCore.Mvc.FromBody] LibreLms.Modules.Management.Endpoints.BulkEnrollmentRequest request) =>
{
    try
    {
        var result = await service.BulkEnrollAsync(request.StudentIds, request.CourseId);
        return Results.Ok(new { enrolled = result.Enrolled, skipped = result.Skipped, errors = result.Errors, errorMessages = result.ErrorMessages });
    }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

adminEnrollments.MapDelete("/{id:guid}", async (
    LibreLms.Modules.Management.Application.AdminEnrollmentService service, Guid id) =>
{
    try { await service.CancelEnrollmentAsync(id); return Results.NoContent(); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});

// === Dev outbox (Development only — spec 027 FR-020) ===
// Deterministic link-extraction surface for humans and Playwright E2E tests.
app.MapGet("/api/dev/outbox", (LibreLms.Host.Mail.DevEmailOutbox outbox, IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment())
        return Results.NotFound();

    var emails = outbox.List().Select(e => new
    {
        to = e.Email.To,
        purpose = e.Email.Purpose.ToString(),
        subject = e.Email.Subject,
        body = e.Email.Body,
        sentAtUtc = e.SentAtUtc.UtcDateTime.ToString("o")
    });
    return Results.Json(emails);
});

// Map Razor Pages
app.MapRazorPages();

// Root redirect
app.MapGet("/", () => Results.Redirect("/Courses"));

app.Run();
