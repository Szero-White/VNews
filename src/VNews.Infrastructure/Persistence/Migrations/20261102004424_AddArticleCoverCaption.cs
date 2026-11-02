using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleCoverCaption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverCaption",
                table: "Articles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverCaption",
                table: "Articles");
        }
    }
}
