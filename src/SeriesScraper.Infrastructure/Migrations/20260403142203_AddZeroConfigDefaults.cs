using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZeroConfigDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "settings",
                columns: new[] { "key", "description", "last_modified_at", "value" },
                values: new object[,]
                {
                    { "forum.default_encoding", "Default character encoding for forum pages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "windows-1250" },
                    { "ForumRefreshIntervalHours", "Interval between forum structure refreshes (hours)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24" },
                    { "imdb.refresh_interval", "Interval between IMDB dataset refreshes (hours, 168 = 7 days)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "168" },
                    { "language.filter", "Language filter for results (all = no filter)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "all" },
                    { "quality.patterns", "Quality token patterns (pre-seeded via quality_patterns table)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { "results.page_size", "Number of results displayed per page", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "25" },
                    { "scrape.request_delay", "Delay between scrape requests in milliseconds", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2000" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "forum.default_encoding");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "ForumRefreshIntervalHours");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "imdb.refresh_interval");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "language.filter");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "results.page_size");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "quality.patterns");

            migrationBuilder.DeleteData(
                table: "settings",
                keyColumn: "key",
                keyValue: "scrape.request_delay");
        }
    }
}
