using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreLms.Host.Migrations.Enrollment
{
    /// <inheritdoc />
    public partial class AddAvatarPathToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarPath",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarPath",
                table: "Students");
        }
    }
}
