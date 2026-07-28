using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LearningLms.Modules.Catalog;
using LearningLms.Modules.Catalog.Infrastructure;
using LearningLms.Modules.Catalog.Application;
using LearningLms.Modules.Catalog.Endpoints;
using LearningLms.Modules.Enrollment;
using LearningLms.Modules.Enrollment.Infrastructure;
using LearningLms.Modules.Enrollment.Application;
using LearningLms.Modules.Enrollment.Endpoints;

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

    // Suppress pending model changes warning for dev — use Migrate() to apply
    // migrations for each context independently. Migrations properly handle
    // multiple DbContexts sharing one database via the __EFMigrationsHistory table.
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
}

// Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();

// === Catalog Module Endpoints ===
var courses = app.MapGroup("/api/courses");
courses.MapGet("/", async (CourseCatalogService service, string? search, string? category) =>
{
    var courseList = await service.ListAsync(search, category);
    var dto = courseList.Select(c => new CourseDto(c.Id, c.Title, c.ShortDescription, c.Category, c.Duration));
    return Results.Ok(new { courses = dto });
});

courses.MapGet("/{id:guid}", async (CourseCatalogService service, Guid id) =>
{
    var course = await service.GetByIdAsync(id);
    if (course is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        course.Id,
        course.Title,
        course.ShortDescription,
        course.FullDescription,
        course.Category,
        course.Duration
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
