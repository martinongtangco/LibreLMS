using LibreLms.Modules.Catalog.Domain;

namespace LibreLms.Modules.Catalog.Infrastructure;

/// <summary>Seeds sample courses across multiple categories for demonstration.</summary>
public static class CatalogSeeder
{
    public static void Seed(CatalogDbContext context)
    {
        var courses = new[]
        {
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Introduction to C#",
                ShortDescription = "Learn the basics of C# programming including variables, types, and control flow.",
                FullDescription = "A comprehensive introduction to C# programming language. Covers fundamentals like variables, data types, control structures, methods, and object-oriented programming basics. Perfect for beginners starting their programming journey.",
                Category = "Programming",
                Duration = "3 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
                Title = "Advanced .NET Patterns",
                ShortDescription = "Explore advanced design patterns and best practices in .NET development.",
                FullDescription = "Dive deep into advanced .NET development patterns including dependency injection, middleware pipelines, the repository pattern, and clean architecture principles. Includes hands-on exercises with real-world scenarios.",
                Category = "Programming",
                Duration = "5 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
                Title = "Web Development with ASP.NET Core",
                ShortDescription = "Build modern web applications using ASP.NET Core MVC and minimal APIs.",
                FullDescription = "Learn to build full-featured web applications with ASP.NET Core. Covers Razor Pages, MVC, minimal APIs, middleware, authentication, and deployment. Includes practical projects and real-world patterns.",
                Category = "Programming",
                Duration = "6 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111114"),
                Title = "Database Design Fundamentals",
                ShortDescription = "Master relational database design, normalization, and query optimization.",
                FullDescription = "Learn the principles of relational database design including normalization, indexing strategies, query optimization, and EF Core integration. Practical exercises with SQL Server.",
                Category = "Database",
                Duration = "4 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111115"),
                Title = "UI/UX Design Principles",
                ShortDescription = "Understand core principles of user interface and experience design.",
                FullDescription = "Explore the fundamentals of UI/UX design including color theory, typography, layout principles, accessibility, and user-centered design methodology. Includes practical wireframing exercises.",
                Category = "Design",
                Duration = "3 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
                Title = "Responsive Web Design",
                ShortDescription = "Create websites that look great on any device using modern CSS techniques.",
                FullDescription = "Master responsive web design with CSS Grid, Flexbox, media queries, and modern layout techniques. Learn to build interfaces that adapt seamlessly across desktop, tablet, and mobile devices.",
                Category = "Design",
                Duration = "4 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111117"),
                Title = "Git Version Control",
                ShortDescription = "Learn Git fundamentals for effective source control and collaboration.",
                FullDescription = "Master Git version control from basics to advanced workflows. Covers branching strategies, merge conflicts, rebasing, pull requests, and collaboration patterns used in professional development teams.",
                Category = "Tools",
                Duration = "2 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111118"),
                Title = "Docker and Container Basics",
                ShortDescription = "Containerize applications using Docker for consistent development and deployment.",
                FullDescription = "Learn Docker fundamentals including Dockerfiles, images, containers, Docker Compose, and best practices for containerized applications. Hands-on labs with real project scenarios.",
                Category = "Tools",
                Duration = "3 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111119"),
                Title = "Introduction to SQL",
                ShortDescription = "Write effective SQL queries for data retrieval, filtering, and aggregation.",
                FullDescription = "Start your SQL journey with fundamentals of relational databases. Learn SELECT, JOIN, WHERE, GROUP BY, subqueries, and window functions with practical examples using SQL Server.",
                Category = "Database",
                Duration = "3 hours"
            },
            new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111120"),
                Title = "REST API Design",
                ShortDescription = "Design clean, maintainable REST APIs following industry best practices.",
                FullDescription = "Learn RESTful API design principles including resource naming, HTTP methods, status codes, versioning, pagination, and error handling. Build production-ready APIs with ASP.NET Core.",
                Category = "Programming",
                Duration = "4 hours"
            }
        };

        context.Courses.AddRange(courses);
        context.SaveChanges();
    }
}
