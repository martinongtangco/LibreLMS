using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Scorm
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScormPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManifestTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LaunchPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentDirectory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScormPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScoreRaw = table.Column<double>(type: "float", nullable: true),
                    SessionTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SuspendData = table.Column<string>(type: "nvarchar(65536)", maxLength: 65536, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCommitAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScormPackages_CourseId",
                table: "ScormPackages",
                column: "CourseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseAttempts_StudentId_CourseId_AttemptNumber",
                table: "CourseAttempts",
                columns: new[] { "StudentId", "CourseId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseAttempts_StudentId_CourseId",
                table: "CourseAttempts",
                columns: new[] { "StudentId", "CourseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScormPackages");
            migrationBuilder.DropTable(name: "CourseAttempts");
        }
    }
}
