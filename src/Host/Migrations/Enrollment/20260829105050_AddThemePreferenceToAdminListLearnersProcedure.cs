using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Enrollment
{
    /// <summary>
    /// Re-creates the AdminListLearners stored procedure adding s.ThemePreference
    /// (spec 042) as the 9th column of the learner-row SELECT. The total-count
    /// SELECT is unchanged. House pattern: idempotent DROP + CREATE (see
    /// 20260822123208_AddAdminListLearnersProcedure).
    /// </summary>
    public partial class AddThemePreferenceToAdminListLearnersProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('AdminListLearners', 'P') IS NOT NULL DROP PROCEDURE AdminListLearners;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE AdminListLearners
    @Search NVARCHAR(200) = NULL,
    @Role NVARCHAR(50) = NULL,
    @PageSize INT = 10,
    @PageNumber INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageSize <= 0 SET @PageSize = 10;
    IF @PageNumber <= 0 SET @PageNumber = 1;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, s.CreatedAt, s.IsEmailVerified, s.AvatarPath, s.ThemePreference
    FROM Students s
    WHERE (@Search IS NULL OR @Search = ''
           OR s.Name LIKE '%' + @Search + '%' OR s.Email LIKE '%' + @Search + '%')
        AND (@Role IS NULL OR @Role = '' OR s.Roles = @Role)
    ORDER BY s.Name ASC, s.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM Students s
    WHERE (@Search IS NULL OR @Search = ''
           OR s.Name LIKE '%' + @Search + '%' OR s.Email LIKE '%' + @Search + '%')
        AND (@Role IS NULL OR @Role = '' OR s.Roles = @Role);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AdminListLearners;");
        }
    }
}
