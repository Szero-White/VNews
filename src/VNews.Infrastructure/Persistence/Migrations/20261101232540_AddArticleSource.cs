using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "Articles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Articles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_SourceUrl",
                table: "Articles",
                column: "SourceUrl",
                unique: true,
                filter: "[SourceUrl] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_SourceUrl",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Articles");
        }
    }
}
