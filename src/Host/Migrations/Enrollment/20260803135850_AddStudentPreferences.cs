using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Enrollment
{
    /// <inheritdoc />
    public partial class AddStudentPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemePreference",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "System");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ThemePreference",
                table: "Students");
        }
    }
}
