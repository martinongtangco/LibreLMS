using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Host.Migrations.Catalog
{
    /// <summary>
    /// Create Full-Text Catalog, Full-Text Index on Courses.Title,
    /// and the BrowseCourses stored procedure for search/filter/pagination.
    /// </summary>
    public partial class AddFullTextIndexAndBrowseProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the BrowseCourses stored procedure (LIKE-based search).
            // Full-text search was removed because the devcontainer MSSQL image
            // does not support fulltext indexes, and the TRY/CATCH wrapper was
            // still crashing EF Core migration execution.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS BrowseCourses;");
        }
    }
}
