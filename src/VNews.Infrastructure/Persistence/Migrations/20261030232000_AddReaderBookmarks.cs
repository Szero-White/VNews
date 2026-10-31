using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VNews.Infrastructure.Persistence;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations;

[DbContext(typeof(VNewsDbContext))]
[Migration("20261030232000_AddReaderBookmarks")]
public partial class AddReaderBookmarks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavedArticles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ArticleId = table.Column<int>(type: "int", nullable: false),
                SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedArticles", x => new { x.UserId, x.ArticleId });
                table.ForeignKey("FK_SavedArticles_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SavedArticles_Articles_ArticleId", x => x.ArticleId, "Articles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_SavedArticles_ArticleId", "SavedArticles", "ArticleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "SavedArticles");
}
