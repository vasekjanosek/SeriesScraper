using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbRefreshIntervalSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "settings",
                columns: new[] { "key", "description", "last_modified_at", "value" },
                values: new object[] { "imdb.refresh_interval", "IMDB refresh schedule: daily, weekly, monthly, or manual", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "weekly" });

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "ImdbRefreshIntervalHours");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "imdb.refresh_interval");

            migrationBuilder.InsertData(
                table: "settings",
                columns: new[] { "key", "description", "last_modified_at", "value" },
                values: new object[] { "ImdbRefreshIntervalHours", "Interval between IMDB dataset refreshes (hours)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24" });
        }
    }
}
