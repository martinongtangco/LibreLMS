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
            // Create Full-Text Catalog and Index with TRY/CATCH for environments without FTS
            // The stored procedure falls back to LIKE search when FTS is unavailable
            migrationBuilder.Sql(@"
                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'LearningLmsFtCatalog')
                        CREATE FULLTEXT CATALOG LearningLmsFtCatalog AS DEFAULT;
                END TRY
                BEGIN CATCH
                    PRINT 'Full-Text Catalog creation failed — search will use LIKE fallback. ' + ERROR_MESSAGE();
                END CATCH;

                BEGIN TRY
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                                   JOIN sys.tables t ON fti.object_id = t.object_id
                                   WHERE t.name = 'Courses')
                    BEGIN
                        CREATE FULLTEXT INDEX ON Courses(Title)
                        KEY INDEX PK_Courses
                        ON LearningLmsFtCatalog
                        WITH (CHANGE_TRACKING = AUTO);
                    END;
                END TRY
                BEGIN CATCH
                    PRINT 'Full-Text Index creation failed — search will use LIKE fallback. ' + ERROR_MESSAGE();
                END CATCH;
            ", suppressTransaction: true);

            // Create the BrowseCourses stored procedure (LIKE-based search with FTS detection at runtime)
            // Uses dynamic SQL internally to avoid CONTAINS validation at CREATE time
            migrationBuilder.Sql("IF OBJECT_ID('BrowseCourses', 'P') IS NOT NULL DROP PROCEDURE BrowseCourses;", suppressTransaction: true);
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

                    -- Check if FTS is available
                    DECLARE @FtsAvailable BIT = 0;
                    SELECT @FtsAvailable = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
                    FROM sys.fulltext_indexes fti
                    JOIN sys.tables t ON fti.object_id = t.object_id
                    WHERE t.name = 'Courses';

                    DECLARE @Sql NVARCHAR(MAX);

                    IF @FtsAvailable = 1 AND (@SearchTerm IS NOT NULL AND @SearchTerm <> '')
                    BEGIN
                        -- FTS available and search term provided: use CONTAINS
                        SET @Sql = N'
                            SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration
                            FROM Courses c
                            WHERE (@Category IS NULL OR @Category = '''' OR c.Category = @Category)
                                AND CONTAINS(c.Title, @SearchTerm)
                            ORDER BY c.Title ASC
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                            SELECT COUNT(*) AS TotalCount
                            FROM Courses c
                            WHERE (@Category IS NULL OR @Category = '''' OR c.Category = @Category)
                                AND CONTAINS(c.Title, @SearchTerm);';
                    END
                    ELSE
                    BEGIN
                        -- FTS not available or no search term: use LIKE
                        SET @Sql = N'
                            SELECT c.Id, c.Title, c.ShortDescription, c.Category, c.Duration
                            FROM Courses c
                            WHERE (@Category IS NULL OR @Category = '''' OR c.Category = @Category)
                                AND (@SearchTerm IS NULL OR @SearchTerm = '''' OR c.Title LIKE ''%'' + @SearchTerm + ''%'')
                            ORDER BY c.Title ASC
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                            SELECT COUNT(*) AS TotalCount
                            FROM Courses c
                            WHERE (@Category IS NULL OR @Category = '''' OR c.Category = @Category)
                                AND (@SearchTerm IS NULL OR @SearchTerm = '''' OR c.Title LIKE ''%'' + @SearchTerm + ''%'');';
                    END;

                    EXEC sp_executesql @Sql,
                        N'@Category NVARCHAR(100), @SearchTerm NVARCHAR(200), @Offset INT, @PageSize INT',
                        @Category = @Category,
                        @SearchTerm = @SearchTerm,
                        @Offset = @Offset,
                        @PageSize = @PageSize;
                END;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop stored procedure
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS BrowseCourses;");

            // Drop Full-Text Index and Catalog (with TRY/CATCH for environments without FTS)
            migrationBuilder.Sql(@"
                BEGIN TRY
                    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes fti
                               JOIN sys.tables t ON fti.object_id = t.object_id
                               WHERE t.name = 'Courses')
                    BEGIN
                        DROP FULLTEXT INDEX ON Courses;
                    END;
                END TRY
                BEGIN CATCH
                    PRINT 'Could not drop FTS index: ' + ERROR_MESSAGE();
                END CATCH;

                BEGIN TRY
                    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'LearningLmsFtCatalog')
                        DROP FULLTEXT CATALOG LearningLmsFtCatalog;
                END TRY
                BEGIN CATCH
                    PRINT 'Could not drop FTS catalog: ' + ERROR_MESSAGE();
                END CATCH;
            ", suppressTransaction: true);
        }
    }
}
