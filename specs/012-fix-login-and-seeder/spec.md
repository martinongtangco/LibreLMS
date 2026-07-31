# Spec 012: Fix Login, Seeder, and Connection String

## Problems

### Bug 1: Connection string name mismatch

Code uses `GetConnectionString("DefaultConnection")` but the environment provides `ConnectionStrings__Sql`. The app connects to a fallback database with no seeded data.

### Bug 2: Login fails with "An error occurred during login"

After successful authentication, `Login.OnPostAsync()` calls `Response.Redirect("/")` which throws `ThreadAbortException`. The catch block swallows it.

### Bug 3: Enrollment seeder never runs

`ManagementSeeder.Seed()` creates a `Student` in `EnrollmentDbContext`. The subsequent guard `!enrollmentCtx.Students.Any()` returns `false`, so `EnrollmentSeeder.Seed()` is never called.

## Proposed Fixes

1. Change all `GetConnectionString("DefaultConnection")` to `GetConnectionString("Sql")`
2. Change `Login.OnPostAsync()` to return `Task<IActionResult>` and use `return Redirect("/")`
3. Change enrollment seeding check to look for specific student email
4. Use `"Cookie"` scheme name in login to match registration

## Files Changed

- `src/Host/Program.cs` (fix connection string + seeding guard)
- `src/Host/Pages/Account/Login.cshtml.cs` (fix redirect + scheme name)
