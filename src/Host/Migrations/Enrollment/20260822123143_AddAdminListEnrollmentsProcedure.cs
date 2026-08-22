using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Enrollment
{
    /// <summary>
    /// Creates the AdminListEnrollments stored procedure (spec 032): paged admin enrollment
    /// listing with student-name and course-title filters.
    /// </summary>
    public partial class AddAdminListEnrollmentsProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the AdminListEnrollments stored procedure (LIKE-based filters).
            migrationBuilder.Sql("IF OBJECT_ID('AdminListEnrollments', 'P') IS NOT NULL DROP PROCEDURE AdminListEnrollments;");
            migrationBuilder.Sql(@"CREATE PROCEDURE AdminListEnrollments
    @StudentName NVARCHAR(200) = NULL,
    @CourseTitle NVARCHAR(200) = NULL,
    @PageSize INT = 10,
    @PageNumber INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageSize <= 0 SET @PageSize = 10;
    IF @PageNumber <= 0 SET @PageNumber = 1;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT e.Id, s.Id, s.Name, s.Email, c.Id, c.Title, s.OrganizationId, e.EnrolledAt
    FROM Enrollments e
    INNER JOIN Students s ON e.StudentId = s.Id
    INNER JOIN Courses c ON e.CourseId = c.Id
    WHERE (@StudentName IS NULL OR @StudentName = '' OR s.Name LIKE '%' + @StudentName + '%')
        AND (@CourseTitle IS NULL OR @CourseTitle = '' OR c.Title LIKE '%' + @CourseTitle + '%')
    ORDER BY e.EnrolledAt DESC, e.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM Enrollments e
    INNER JOIN Students s ON e.StudentId = s.Id
    INNER JOIN Courses c ON e.CourseId = c.Id
    WHERE (@StudentName IS NULL OR @StudentName = '' OR s.Name LIKE '%' + @StudentName + '%')
        AND (@CourseTitle IS NULL OR @CourseTitle = '' OR c.Title LIKE '%' + @CourseTitle + '%');
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AdminListEnrollments;");
        }
    }
}
