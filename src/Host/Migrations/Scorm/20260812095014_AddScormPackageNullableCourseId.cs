using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Scorm
{
    /// <inheritdoc />
    public partial class AddScormPackageNullableCourseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScormPackages_CourseId",
                table: "ScormPackages");

            migrationBuilder.AlterColumn<Guid>(
                name: "CourseId",
                table: "ScormPackages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ScormPackages_CourseId",
                table: "ScormPackages",
                column: "CourseId",
                unique: true,
                filter: "[CourseId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScormPackages_CourseId",
                table: "ScormPackages");

            migrationBuilder.AlterColumn<Guid>(
                name: "CourseId",
                table: "ScormPackages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScormPackages_CourseId",
                table: "ScormPackages",
                column: "CourseId",
                unique: true);
        }
    }
}
