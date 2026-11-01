using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VNews.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Articles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Articles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeDeletion",
                table: "Articles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "StatusBeforeDeletion",
                table: "Articles");
        }
    }
}
