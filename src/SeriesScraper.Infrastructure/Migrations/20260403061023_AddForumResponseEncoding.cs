using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForumResponseEncoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "response_encoding",
                table: "forums",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "utf-8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "response_encoding",
                table: "forums");
        }
    }
}
