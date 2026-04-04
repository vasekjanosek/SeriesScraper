using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "watchlist_notifications",
                columns: table => new
                {
                    watchlist_notification_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    watchlist_item_id = table.Column<int>(type: "integer", nullable: false),
                    link_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_watchlist_notifications", x => x.watchlist_notification_id);
                    table.ForeignKey(
                        name: "fk_watchlist_notifications_links_link_id",
                        column: x => x.link_id,
                        principalTable: "links",
                        principalColumn: "link_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_watchlist_notifications_watchlist_items_watchlist_item_id",
                        column: x => x.watchlist_item_id,
                        principalTable: "watchlist_items",
                        principalColumn: "watchlist_item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_notifications_link_id",
                table: "watchlist_notifications",
                column: "link_id");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistNotifications_IsRead",
                table: "watchlist_notifications",
                column: "is_read");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistNotifications_WatchlistItemId",
                table: "watchlist_notifications",
                column: "watchlist_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistNotifications_WatchlistItemId_LinkId",
                table: "watchlist_notifications",
                columns: new[] { "watchlist_item_id", "link_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "watchlist_notifications");
        }
    }
}
