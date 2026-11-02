using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleViews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    ViewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VisitorHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsMobile = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleViews_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleViews_ArticleId_VisitorHash_ViewedAtUtc",
                table: "ArticleViews",
                columns: new[] { "ArticleId", "VisitorHash", "ViewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleViews_ViewedAtUtc",
                table: "ArticleViews",
                column: "ViewedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleViews");
        }
    }
}
