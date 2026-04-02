using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_types",
                columns: table => new
                {
                    content_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_types", x => x.content_type_id);
                });

            migrationBuilder.CreateTable(
                name: "data_sources",
                columns: table => new
                {
                    source_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_sources", x => x.source_id);
                });

            migrationBuilder.CreateTable(
                name: "forums",
                columns: table => new
                {
                    forum_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    credential_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    crawl_depth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    politeness_delay_ms = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forums", x => x.forum_id);
                });

            migrationBuilder.CreateTable(
                name: "link_types",
                columns: table => new
                {
                    link_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url_pattern = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    icon_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_link_types", x => x.link_type_id);
                });

            migrationBuilder.CreateTable(
                name: "quality_tokens",
                columns: table => new
                {
                    token_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quality_rank = table.Column<int>(type: "integer", nullable: false),
                    polarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_tokens", x => x.token_id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "media_titles",
                columns: table => new
                {
                    media_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    canonical_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_titles", x => x.media_id);
                    table.ForeignKey(
                        name: "fk_media_titles_data_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "data_sources",
                        principalColumn: "source_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forum_sections",
                columns: table => new
                {
                    section_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    forum_id = table.Column<int>(type: "integer", nullable: false),
                    parent_section_id = table.Column<int>(type: "integer", nullable: true),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    detected_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    content_type_id = table.Column<int>(type: "integer", nullable: true),
                    last_crawled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forum_sections", x => x.section_id);
                    table.ForeignKey(
                        name: "fk_forum_sections_content_types_content_type_id",
                        column: x => x.content_type_id,
                        principalTable: "content_types",
                        principalColumn: "content_type_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_forum_sections_forum_sections_parent_section_id",
                        column: x => x.parent_section_id,
                        principalTable: "forum_sections",
                        principalColumn: "section_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forum_sections_forums_forum_id",
                        column: x => x.forum_id,
                        principalTable: "forums",
                        principalColumn: "forum_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scrape_runs",
                columns: table => new
                {
                    run_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    forum_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_items = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processed_items = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scrape_runs", x => x.run_id);
                    table.ForeignKey(
                        name: "fk_scrape_runs_forums_forum_id",
                        column: x => x.forum_id,
                        principalTable: "forums",
                        principalColumn: "forum_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "links",
                columns: table => new
                {
                    link_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    link_type_id = table.Column<int>(type: "integer", nullable: false),
                    parsed_season = table.Column<int>(type: "integer", nullable: true),
                    parsed_episode = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    run_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_links", x => x.link_id);
                    table.ForeignKey(
                        name: "fk_links_link_types_link_type_id",
                        column: x => x.link_type_id,
                        principalTable: "link_types",
                        principalColumn: "link_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_links_scrape_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "scrape_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "content_types",
                columns: new[] { "content_type_id", "name" },
                values: new object[,]
                {
                    { 1, "TV Series" },
                    { 2, "Movie" },
                    { 3, "Other" }
                });

            migrationBuilder.InsertData(
                table: "data_sources",
                columns: new[] { "source_id", "name" },
                values: new object[] { 1, "IMDB" });

            migrationBuilder.InsertData(
                table: "link_types",
                columns: new[] { "link_type_id", "icon_class", "is_active", "is_system", "name", "url_pattern" },
                values: new object[,]
                {
                    { 1, null, true, true, "Direct HTTP", "^https?://" },
                    { 2, null, true, true, "Torrent File", "\\.torrent$" },
                    { 3, null, true, true, "Magnet URI", "^magnet:\\?" },
                    { 4, null, true, true, "Cloud Storage URL", "(drive\\.google|dropbox|mega\\.nz)" }
                });

            migrationBuilder.InsertData(
                table: "quality_tokens",
                columns: new[] { "token_id", "is_active", "polarity", "quality_rank", "token_text" },
                values: new object[,]
                {
                    { 1, true, "Positive", 100, "2160p" },
                    { 2, true, "Positive", 100, "4K" },
                    { 3, true, "Positive", 80, "1080p" },
                    { 4, true, "Positive", 60, "720p" },
                    { 5, true, "Positive", 40, "480p" },
                    { 6, true, "Positive", 70, "BluRay" },
                    { 7, true, "Positive", 50, "WEB-DL" },
                    { 8, true, "Positive", 65, "HEVC" },
                    { 9, true, "Positive", 65, "x265" },
                    { 10, true, "Positive", 60, "x264" },
                    { 11, true, "Positive", 75, "HDR" },
                    { 12, true, "Positive", 50, "SDR" },
                    { 13, true, "Negative", -10, "AI-upscaled" },
                    { 14, true, "Negative", -10, "AI upscale" }
                });

            migrationBuilder.InsertData(
                table: "settings",
                columns: new[] { "key", "description", "last_modified_at", "value" },
                values: new object[,]
                {
                    { "BulkImportMemoryCeilingMB", "Memory ceiling for bulk IMDB imports", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "256" },
                    { "ForumStructureRefreshIntervalHours", "Interval between forum structure refreshes (hours)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24" },
                    { "HttpCircuitBreakerThreshold", "Failures before circuit breaker opens", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "5" },
                    { "HttpRetryBackoffMultiplier", "Backoff multiplier for HTTP retries", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2" },
                    { "HttpRetryCount", "Number of HTTP request retries on failure", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3" },
                    { "HttpTimeoutSeconds", "HTTP request timeout in seconds", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "30" },
                    { "ImdbRefreshIntervalHours", "Interval between IMDB dataset refreshes (hours)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24" },
                    { "MaxConcurrentScrapeThreads", "Maximum number of concurrent scraping threads", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "1" },
                    { "QualityPruningThreshold", "Patterns with hit_count below this are candidates for pruning", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "5" },
                    { "ResultRetentionDays", "Days to retain results (0 = retain all)", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_types_name",
                table: "content_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_sources_name",
                table: "data_sources",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forum_sections_content_type_id",
                table: "forum_sections",
                column: "content_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_sections_forum_id",
                table: "forum_sections",
                column: "forum_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_sections_parent_section_id",
                table: "forum_sections",
                column: "parent_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_sections_url",
                table: "forum_sections",
                column: "url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_link_types_name",
                table: "link_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_links_link_type_id",
                table: "links",
                column: "link_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_links_run_id",
                table: "links",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_titles_source_id",
                table: "media_titles",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_MediaTitles_TitleMatching",
                table: "media_titles",
                columns: new[] { "canonical_title", "year", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_quality_tokens_token_text",
                table: "quality_tokens",
                column: "token_text",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scrape_runs_forum_id",
                table: "scrape_runs",
                column: "forum_id");

            // Partial indexes via raw SQL (AC#1)
            migrationBuilder.Sql(
                "CREATE INDEX ix_quality_tokens_is_active_partial ON quality_tokens (token_text) WHERE is_active = true;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_link_types_is_active_partial ON link_types (name) WHERE is_active = true;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_links_is_current_partial ON links (run_id, link_type_id) WHERE is_current = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop partial indexes first
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_links_is_current_partial;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_link_types_is_active_partial;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_quality_tokens_is_active_partial;");

            migrationBuilder.DropTable(
                name: "forum_sections");

            migrationBuilder.DropTable(
                name: "links");

            migrationBuilder.DropTable(
                name: "media_titles");

            migrationBuilder.DropTable(
                name: "quality_tokens");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "content_types");

            migrationBuilder.DropTable(
                name: "link_types");

            migrationBuilder.DropTable(
                name: "scrape_runs");

            migrationBuilder.DropTable(
                name: "data_sources");

            migrationBuilder.DropTable(
                name: "forums");
        }
    }
}
