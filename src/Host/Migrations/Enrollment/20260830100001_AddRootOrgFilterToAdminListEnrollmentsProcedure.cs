using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Enrollment
{
    /// <summary>
    /// Re-creates the AdminListEnrollments stored procedure adding the org-scope
    /// parameter @RootOrgId UNIQUEIDENTIFIER = NULL (spec 052, ADR 0010):
    /// null = system-wide (SuperUser); otherwise both the page SELECT and the
    /// total-count SELECT are restricted to enrollments whose STUDENT's
    /// OrganizationId is in the root org's subtree (the enrollment scope is
    /// one-dimensional: the enrolled student's org). Column set unchanged.
    /// House pattern: idempotent DROP + CREATE.
    /// </summary>
    public partial class AddRootOrgFilterToAdminListEnrollmentsProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('AdminListEnrollments', 'P') IS NOT NULL DROP PROCEDURE AdminListEnrollments;");
            migrationBuilder.Sql(@"CREATE PROCEDURE AdminListEnrollments
    @StudentName NVARCHAR(200) = NULL,
    @CourseTitle NVARCHAR(200) = NULL,
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

    SELECT e.Id, s.Id, s.Name, s.Email, c.Id, c.Title, s.OrganizationId, e.EnrolledAt
    FROM Enrollments e
    INNER JOIN Students s ON e.StudentId = s.Id
    INNER JOIN Courses c ON e.CourseId = c.Id
    WHERE (@StudentName IS NULL OR @StudentName = '' OR s.Name LIKE '%' + @StudentName + '%')
        AND (@CourseTitle IS NULL OR @CourseTitle = '' OR c.Title LIKE '%' + @CourseTitle + '%')
        AND (@RootOrgId IS NULL OR s.OrganizationId IN (SELECT OrgId FROM #Subtree))
    ORDER BY e.EnrolledAt DESC, e.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM Enrollments e
    INNER JOIN Students s ON e.StudentId = s.Id
    INNER JOIN Courses c ON e.CourseId = c.Id
    WHERE (@StudentName IS NULL OR @StudentName = '' OR s.Name LIKE '%' + @StudentName + '%')
        AND (@CourseTitle IS NULL OR @CourseTitle = '' OR c.Title LIKE '%' + @CourseTitle + '%')
        AND (@RootOrgId IS NULL OR s.OrganizationId IN (SELECT OrgId FROM #Subtree));
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AdminListEnrollments;");
        }
    }
}
