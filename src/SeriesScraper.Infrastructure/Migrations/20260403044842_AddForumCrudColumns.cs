using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForumCrudColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_scrape_runs_forums_forum_id",
                table: "scrape_runs");

            migrationBuilder.AlterColumn<int>(
                name: "forum_id",
                table: "scrape_runs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "forum_name",
                table: "scrape_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_password",
                table: "forums",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_scrape_runs_forums_forum_id",
                table: "scrape_runs",
                column: "forum_id",
                principalTable: "forums",
                principalColumn: "forum_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_scrape_runs_forums_forum_id",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "forum_name",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "encrypted_password",
                table: "forums");

            migrationBuilder.AlterColumn<int>(
                name: "forum_id",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_scrape_runs_forums_forum_id",
                table: "scrape_runs",
                column: "forum_id",
                principalTable: "forums",
                principalColumn: "forum_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
