using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using LearningLms.Modules.Catalog;
using LearningLms.Modules.Catalog.Infrastructure;
using LearningLms.Modules.Catalog.Application;
using LearningLms.Modules.Catalog.Endpoints;
using LearningLms.Modules.Enrollment;
using LearningLms.Modules.Enrollment.Infrastructure;
using LearningLms.Modules.Enrollment.Application;
using LearningLms.Modules.Enrollment.Endpoints;
using LearningLms.Modules.Scorm;
using LearningLms.Modules.Scorm.Application;
using LearningLms.Modules.Scorm.Infrastructure;
using LearningLms.Modules.Scorm.Endpoints;
using static LearningLms.Host.ScormHelpers;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core contexts with MSSQL
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly(typeof(Program).Assembly)));

builder.Services.AddDbContext<EnrollmentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly(typeof(Program).Assembly)));

// Register module services
builder.Services.AddCatalogModule();
builder.Services.AddEnrollmentModule();
builder.Services.AddScormModule();

// Register EF Core context for Scorm
builder.Services.AddDbContext<ScormDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly(typeof(Program).Assembly)));

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
});

// Add Razor Pages and HttpClient
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();

// Ensure database and tables exist, then seed data on startup
using (var scope = app.Services.CreateScope())
{
    var catalogCtx = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var enrollmentCtx = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();

    catalogCtx.Database.EnsureCreated();
    try
    {
        catalogCtx.Database.Migrate();
    }
    catch (System.InvalidOperationException) { /* pending model changes — tables may exist */ }
    enrollmentCtx.Database.Migrate();

    // Seed catalog
    if (!catalogCtx.Courses.Any())
    {
        LearningLms.Modules.Catalog.Infrastructure.CatalogSeeder.Seed(catalogCtx);
    }

    // Seed students
    if (!enrollmentCtx.Students.Any())
    {
        LearningLms.Modules.Enrollment.Infrastructure.EnrollmentSeeder.Seed(enrollmentCtx);
    }

    // Seed Scorm sample package
    var scormCtx = scope.ServiceProvider.GetRequiredService<ScormDbContext>();
    var hostEnv = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    scormCtx.Database.EnsureCreated();
    try
    {
        scormCtx.Database.Migrate();
    }
    catch (System.InvalidOperationException) { /* pending model changes — tables may exist */ }
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

    return Results.Created($"/api/enrollments/{enrollment.Id}", new EnrollmentDto(
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
        entry = result.EntryMode,
        attemptNumber = result.AttemptNumber
    });
});

// POST /api/scorm/upload
scorm.MapPost("/upload", [Authorize] async (
    ScormPackageService packageService,
    HttpContext httpContext,
    IFormCollection form) =>
{
    var isAdmin = httpContext.User.IsInRole("Admin");
    if (!isAdmin)
        return Results.Forbid();

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

// Map Razor Pages
app.MapRazorPages();

// Root redirect
app.MapGet("/", () => Results.Redirect("/Courses"));

app.Run();

/// <summary>Extract student ID from HTTP context (claims or demo fallback).</summary>
static Guid GetStudentId(HttpContext httpContext)
{
    var claim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? httpContext.User.FindFirst("sub")?.Value;

    if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var parsedGuid))
        return parsedGuid;

    // Demo fallback: use first seeded student
    return Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
}
