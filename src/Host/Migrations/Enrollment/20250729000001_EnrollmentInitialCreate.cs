using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningLms.Host.Migrations.Enrollment
{
    /// <summary>
    /// Initial migration for the Enrollment module — creates Students and Enrollments tables.
    /// Scaffolded from EnrollmentDbContext entity configurations.
    /// Apply: dotnet ef database update --context EnrollmentDbContext --project src/Host
    /// </summary>
    [Migration("20250729000001_EnrollmentInitialCreate")]
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                });

            // Unique email constraint
            migrationBuilder.CreateIndex(
                name: "IX_Students_Email",
                table: "Students",
                column: "Email",
                unique: true);

            // FR-005: Prevent duplicate enrollment at the database level
            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_CourseId",
                table: "Enrollments",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Enrollments");
            migrationBuilder.DropTable(name: "Students");
        }
    }
}
