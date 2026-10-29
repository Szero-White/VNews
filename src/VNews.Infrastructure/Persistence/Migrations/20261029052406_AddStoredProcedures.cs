using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds two parameterized SQL Server stored procedures used by VNews.Infrastructure.Services.ArticleStoredProcedureService:
    /// dbo.sp_GetPublishedArticlesByCategory (paginated published articles by category slug)
    /// and dbo.sp_GetDailyPublishStats (daily published-article counts for a date range).
    /// </summary>
    public partial class AddStoredProcedures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.sp_GetPublishedArticlesByCategory
    @CategorySlug NVARCHAR(100),
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 50 SET @PageSize = 10;

    SELECT
        a.Id AS ArticleId,
        a.Title,
        a.Slug,
        a.Summary,
        a.CoverImageUrl,
        a.AuthorName,
        a.PublishedAt,
        c.Name AS CategoryName,
        c.Slug AS CategorySlug,
        COUNT(*) OVER() AS TotalCount
    FROM dbo.Articles a
    INNER JOIN dbo.Categories c ON c.Id = a.CategoryId
    WHERE c.Slug = @CategorySlug AND a.Status = N'Published'
    ORDER BY a.PublishedAt DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.sp_GetDailyPublishStats
    @FromDate DATE,
    @ToDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(a.PublishedAt AS DATE) AS PublishDate,
        COUNT(*) AS PublishedCount
    FROM dbo.Articles a
    WHERE a.Status = N'Published'
      AND a.PublishedAt >= @FromDate
      AND a.PublishedAt < DATEADD(DAY, 1, @ToDate)
    GROUP BY CAST(a.PublishedAt AS DATE)
    ORDER BY PublishDate;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_GetPublishedArticlesByCategory;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_GetDailyPublishStats;");
        }
    }
}
