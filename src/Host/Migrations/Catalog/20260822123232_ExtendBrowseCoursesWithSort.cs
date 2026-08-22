using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Catalog
{
    /// <summary>
    /// Extends the BrowseCourses stored procedure (spec 032): adds optional
    /// @SortBy/@SortDirection parameters and the OrganizationId result column; defaults preserve the legacy behavior.
    /// </summary>
    public partial class ExtendBrowseCoursesWithSort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('BrowseCourses', 'P') IS NOT NULL DROP PROCEDURE BrowseCourses;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE BrowseCourses
                    @SearchTerm NVARCHAR(200) = NULL,
                    @Category NVARCHAR(100) = NULL,
                    @PageSize INT = 10,
                    @PageNumber INT = 1,
                    @SortBy NVARCHAR(20) = N'title',
                    @SortDirection NVARCHAR(4) = N'asc'
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @PageSize <= 0 SET @PageSize = 10;
                    IF @PageNumber <= 0 SET @PageNumber = 1;

                    -- Normalize sort inputs to the allowed set (unknown values fall back to defaults).
                    IF @SortBy IS NULL OR @SortBy NOT IN (N'title', N'category', N'duration') SET @SortBy = N'title';
                    IF @SortDirection IS NULL OR @SortDirection NOT IN (N'asc', N'desc') SET @SortDirection = N'asc';

                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

                    SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration, c.OrganizationId
                    FROM Courses c
                    WHERE (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.Title LIKE '%' + @SearchTerm + '%')
                    ORDER BY
                        CASE WHEN @SortBy = N'title' AND @SortDirection = N'asc' THEN c.Title END ASC,
                        CASE WHEN @SortBy = N'title' AND @SortDirection = N'desc' THEN c.Title END DESC,
                        CASE WHEN @SortBy = N'category' AND @SortDirection = N'asc' THEN c.Category END ASC,
                        CASE WHEN @SortBy = N'category' AND @SortDirection = N'desc' THEN c.Category END DESC,
                        CASE WHEN @SortBy = N'duration' AND @SortDirection = N'asc' THEN c.Duration END ASC,
                        CASE WHEN @SortBy = N'duration' AND @SortDirection = N'desc' THEN c.Duration END DESC,
                        c.Id ASC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                    SELECT COUNT(*) AS TotalCount
                    FROM Courses c
                    WHERE (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.Title LIKE '%' + @SearchTerm + '%');
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('BrowseCourses', 'P') IS NOT NULL DROP PROCEDURE BrowseCourses;");
            migrationBuilder.Sql(@"
                CREATE PROCEDURE BrowseCourses
                    @SearchTerm NVARCHAR(200) = NULL,
                    @Category NVARCHAR(100) = NULL,
                    @PageSize INT = 12,
                    @PageNumber INT = 1
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @PageSize <= 0 SET @PageSize = 12;
                    IF @PageNumber <= 0 SET @PageNumber = 1;

                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

                    SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration
                    FROM Courses c
                    WHERE (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.Title LIKE '%' + @SearchTerm + '%')
                    ORDER BY c.Title ASC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                    SELECT COUNT(*) AS TotalCount
                    FROM Courses c
                    WHERE (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.Title LIKE '%' + @SearchTerm + '%');
                END;
            ");
        }
    }
}
