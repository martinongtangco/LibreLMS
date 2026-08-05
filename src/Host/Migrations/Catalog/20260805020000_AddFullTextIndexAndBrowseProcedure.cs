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
            // Create Full-Text Catalog
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'LearningLmsFtCatalog')
                    CREATE FULLTEXT CATALOG LearningLmsFtCatalog AS DEFAULT;
            ");

            // Create Full-Text Index on Courses.Title using the existing unique index as key index
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                               JOIN sys.tables t ON fti.object_id = t.object_id
                               WHERE t.name = 'Courses')
                    CREATE FULLTEXT INDEX ON Courses(Title)
                    KEY INDEX UK_Title_OrganizationId
                    CATALOG LearningLmsFtCatalog
                    WITH (CHANGE_TRACKING AUTO);
            ");

            // Create the BrowseCourses stored procedure
            migrationBuilder.Sql(@"
                CREATE PROCEDURE BrowseCourses
                    @SearchTerm NVARCHAR(200) = NULL,
                    @Category NVARCHAR(100) = NULL,
                    @OrganizationIdScope UNIQUEIDENTIFIER = NULL,
                    @VisibleCourseIds UNIQUEIDENTIFIER[] = NULL,
                    @PageSize INT = 12,
                    @PageNumber INT = 1
                AS
                BEGIN
                    SET NOCOUNT ON;

                    -- Validate pagination parameters
                    IF @PageSize <= 0 SET @PageSize = 12;
                    IF @PageNumber <= 0 SET @PageNumber = 1;

                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

                    -- Check if FTS index exists on Courses table
                    DECLARE @FtsAvailable BIT = 0;
                    SELECT @FtsAvailable = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
                    FROM sys.fulltext_indexes fti
                    JOIN sys.tables t ON fti.object_id = t.object_id
                    WHERE t.name = 'Courses';

                    -- Result Set 1: Page of courses
                    SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration
                    FROM Courses c
                    WHERE
                        -- Org scope via TVP (if provided)
                        (@VisibleCourseIds IS NULL OR EXISTS (SELECT 1 FROM @VisibleCourseIds v WHERE v.value = c.Id))
                        -- Org scope via single ID (if provided without TVP)
                        (@OrganizationIdScope IS NULL OR c.OrganizationId = @OrganizationIdScope)
                        -- Category filter
                        (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        -- Search filter: FTS or LIKE fallback
                        (
                            @SearchTerm IS NULL
                            OR @SearchTerm = ''
                            OR (
                                (@FtsAvailable = 1 AND CONTAINS(c.Title, @SearchTerm))
                                OR (@FtsAvailable = 0 AND c.Title LIKE '%' + @SearchTerm + '%')
                            )
                        )
                    ORDER BY c.Title ASC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;

                    -- Result Set 2: Total count
                    SELECT COUNT(*) AS TotalCount
                    FROM Courses c
                    WHERE
                        -- Org scope via TVP (if provided)
                        (@VisibleCourseIds IS NULL OR EXISTS (SELECT 1 FROM @VisibleCourseIds v WHERE v.value = c.Id))
                        -- Org scope via single ID (if provided without TVP)
                        (@OrganizationIdScope IS NULL OR c.OrganizationId = @OrganizationIdScope)
                        -- Category filter
                        (@Category IS NULL OR @Category = '' OR c.Category = @Category)
                        -- Search filter: FTS or LIKE fallback
                        (
                            @SearchTerm IS NULL
                            OR @SearchTerm = ''
                            OR (
                                (@FtsAvailable = 1 AND CONTAINS(c.Title, @SearchTerm))
                                OR (@FtsAvailable = 0 AND c.Title LIKE '%' + @SearchTerm + '%')
                            )
                        );
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop stored procedure
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS BrowseCourses;");

            // Drop Full-Text Index
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                           JOIN sys.tables t ON fti.object_id = t.object_id
                           WHERE t.name = 'Courses')
                    DROP FULLTEXT INDEX ON Courses;
            ");

            // Drop Full-Text Catalog
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'LearningLmsFtCatalog')
                    DROP FULLTEXT CATALOG LearningLmsFtCatalog;
            ");
        }
    }
}
