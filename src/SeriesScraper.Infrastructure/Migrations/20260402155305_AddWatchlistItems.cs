using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_links_run_id",
                table: "links");

            migrationBuilder.AddColumn<string>(
                name: "post_url",
                table: "links",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "watchlist_items",
                columns: table => new
                {
                    watchlist_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    media_title_id = table.Column<int>(type: "integer", nullable: true),
                    custom_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notification_preference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_matched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_watchlist_items", x => x.watchlist_item_id);
                    table.ForeignKey(
                        name: "fk_watchlist_items_media_titles_media_title_id",
                        column: x => x.media_title_id,
                        principalTable: "media_titles",
                        principalColumn: "media_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_links_run_id_post_url_is_current",
                table: "links",
                columns: new[] { "run_id", "post_url", "is_current" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_IsActive",
                table: "watchlist_items",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_MediaTitleId",
                table: "watchlist_items",
                column: "media_title_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_links_run_id_post_url_is_current",
                table: "links");

            migrationBuilder.DropColumn(
                name: "post_url",
                table: "links");

            migrationBuilder.CreateIndex(
                name: "ix_links_run_id",
                table: "links",
                column: "run_id");
        }
    }
}
