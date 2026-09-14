using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Enrollment
{
    /// <summary>
    /// Re-creates the AdminListLearners stored procedure adding the org-scope
    /// parameter @RootOrgId UNIQUEIDENTIFIER = NULL (spec 052, ADR 0010):
    /// null = system-wide (SuperUser); otherwise both the page SELECT and the
    /// total-count SELECT are restricted to students whose OrganizationId is in
    /// the root org's subtree (recursive CTE into a #Subtree temp table, which
    /// is always created so the predicate compiles in both modes). The column
    /// set is unchanged (9 learner columns — credential columns stay excluded).
    /// House pattern: idempotent DROP + CREATE.
    /// </summary>
    public partial class AddRootOrgFilterToAdminListLearnersProcedure : Migration
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
    @PageNumber INT = 1,
    @RootOrgId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageSize <= 0 SET @PageSize = 10;
    IF @PageNumber <= 0 SET @PageNumber = 1;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Org-scope subtree (ADR 0010): always created so the predicate below
    -- compiles whether or not @RootOrgId is supplied.
    CREATE TABLE #Subtree (OrgId UNIQUEIDENTIFIER PRIMARY KEY);
    IF @RootOrgId IS NOT NULL
    BEGIN
        ;WITH Subtree AS
        (
            SELECT Id AS OrgId, ParentId
            FROM Organizations
            WHERE Id = @RootOrgId AND IsDeleted = 0
            UNION ALL
            SELECT o.Id, o.ParentId
            FROM Organizations o
            INNER JOIN Subtree p ON o.ParentId = p.OrgId
            WHERE o.IsDeleted = 0
        )
        INSERT INTO #Subtree (OrgId)
        SELECT OrgId FROM Subtree;
    END

    SELECT s.Id, s.Name, s.Email, s.Roles, s.OrganizationId, s.CreatedAt, s.IsEmailVerified, s.AvatarPath, s.ThemePreference
    FROM Students s
    WHERE (@Search IS NULL OR @Search = ''
           OR s.Name LIKE '%' + @Search + '%' OR s.Email LIKE '%' + @Search + '%')
        AND (@Role IS NULL OR @Role = '' OR s.Roles = @Role)
        AND (@RootOrgId IS NULL OR s.OrganizationId IN (SELECT OrgId FROM #Subtree))
    ORDER BY s.Name ASC, s.Id ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM Students s
    WHERE (@Search IS NULL OR @Search = ''
           OR s.Name LIKE '%' + @Search + '%' OR s.Email LIKE '%' + @Search + '%')
        AND (@Role IS NULL OR @Role = '' OR s.Roles = @Role)
        AND (@RootOrgId IS NULL OR s.OrganizationId IN (SELECT OrgId FROM #Subtree));
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
