using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeriesScraper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "links",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language",
                table: "links");
        }
    }
}
