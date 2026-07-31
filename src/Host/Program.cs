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
using static LibreLms.Host.ScormHelpers;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core contexts with MSSQL
void ConfigureDbContext(DbContextOptionsBuilder opts, string? connStr)
{
    opts.UseSqlServer(connStr, sql => sql.MigrationsAssembly(typeof(Program).Assembly));
    opts.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
}

builder.Services.AddDbContext<CatalogDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<EnrollmentDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("DefaultConnection")));

// Register module services
builder.Services.AddCatalogModule();
builder.Services.AddEnrollmentModule();
builder.Services.AddScormModule();

// Register EF Core context for Scorm
builder.Services.AddDbContext<ScormDbContext>(opts => ConfigureDbContext(opts, builder.Configuration.GetConnectionString("DefaultConnection")));

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
    var scormCtx = scope.ServiceProvider.GetRequiredService<ScormDbContext>();
    var hostEnv = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

    // Create database if it doesn't exist
    catalogCtx.Database.EnsureCreated();

    // Drop and recreate database cleanly, then apply all migrations
    catalogCtx.Database.EnsureDeleted();
    catalogCtx.Database.Migrate();
    enrollmentCtx.Database.Migrate();
    scormCtx.Database.Migrate();
    enrollmentCtx.Database.Migrate();
    scormCtx.Database.Migrate();

    // Seed catalog
    if (!catalogCtx.Courses.Any())
    {
        LibreLms.Modules.Catalog.Infrastructure.CatalogSeeder.Seed(catalogCtx);
    }

    // Seed students
    if (!enrollmentCtx.Students.Any())
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
courses.MapPost("/", [Authorize(Roles = "Admin")] async (CourseCatalogService service, [FromBody] LearningLms.Modules.Catalog.Endpoints.CreateCourseRequest request) =>
{
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
        contentUrl = result.ContentUrl,
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
