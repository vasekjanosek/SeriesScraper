using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbImportInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "credential_key",
                table: "forums",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "data_source_import_runs",
                columns: table => new
                {
                    import_run_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rows_imported = table.Column<long>(type: "bigint", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_source_import_runs", x => x.import_run_id);
                    table.ForeignKey(
                        name: "fk_data_source_import_runs_data_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "data_sources",
                        principalColumn: "source_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "imdb_title_akas_staging",
                columns: table => new
                {
                    staging_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordering = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    types = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attributes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_original_title = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_title_akas_staging", x => x.staging_id);
                });

            migrationBuilder.CreateTable(
                name: "imdb_title_basics_staging",
                columns: table => new
                {
                    staging_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    primary_title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    original_title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_adult = table.Column<bool>(type: "boolean", nullable: false),
                    start_year = table.Column<int>(type: "integer", nullable: true),
                    end_year = table.Column<int>(type: "integer", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    genres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_title_basics_staging", x => x.staging_id);
                });

            migrationBuilder.CreateTable(
                name: "imdb_title_episode_staging",
                columns: table => new
                {
                    staging_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    parent_tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: true),
                    episode_number = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_title_episode_staging", x => x.staging_id);
                });

            migrationBuilder.CreateTable(
                name: "imdb_title_ratings_staging",
                columns: table => new
                {
                    staging_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    average_rating = table.Column<decimal>(type: "numeric(3,1)", nullable: false),
                    num_votes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_title_ratings_staging", x => x.staging_id);
                });

            migrationBuilder.CreateTable(
                name: "quality_learned_patterns",
                columns: table => new
                {
                    pattern_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pattern_regex = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    derived_rank = table.Column<int>(type: "integer", nullable: false),
                    hit_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_matched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    algorithm_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    polarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_learned_patterns", x => x.pattern_id);
                });

            migrationBuilder.CreateTable(
                name: "scrape_run_items",
                columns: table => new
                {
                    run_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<int>(type: "integer", nullable: false),
                    post_url = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scrape_run_items", x => x.run_item_id);
                    table.ForeignKey(
                        name: "fk_scrape_run_items_scrape_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "scrape_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "quality_learned_patterns",
                columns: new[] { "pattern_id", "algorithm_version", "derived_rank", "is_active", "last_matched_at", "pattern_regex", "polarity", "source" },
                values: new object[,]
                {
                    { 1, "1.0", 100, true, null, "\\b2160p\\b", "Positive", "Seed" },
                    { 2, "1.0", 100, true, null, "\\b4K\\b", "Positive", "Seed" },
                    { 3, "1.0", 80, true, null, "\\b1080p\\b", "Positive", "Seed" },
                    { 4, "1.0", 60, true, null, "\\b720p\\b", "Positive", "Seed" },
                    { 5, "1.0", 70, true, null, "\\bBluRay\\b", "Positive", "Seed" },
                    { 6, "1.0", 50, true, null, "\\bWEB[-\\s]?DL\\b", "Positive", "Seed" },
                    { 7, "1.0", 65, true, null, "\\bHEVC\\b", "Positive", "Seed" },
                    { 8, "1.0", 65, true, null, "\\bx265\\b", "Positive", "Seed" },
                    { 9, "1.0", -10, true, null, "\\bAI[-\\s]?upscale[d]?\\b", "Negative", "Seed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceImportRuns_SourceStarted",
                table: "data_source_import_runs",
                columns: new[] { "source_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scrape_run_items_run_id",
                table: "scrape_run_items",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_scrape_run_items_run_id_post_url",
                table: "scrape_run_items",
                columns: new[] { "run_id", "post_url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_source_import_runs");

            migrationBuilder.DropTable(
                name: "imdb_title_akas_staging");

            migrationBuilder.DropTable(
                name: "imdb_title_basics_staging");

            migrationBuilder.DropTable(
                name: "imdb_title_episode_staging");

            migrationBuilder.DropTable(
                name: "imdb_title_ratings_staging");

            migrationBuilder.DropTable(
                name: "quality_learned_patterns");

            migrationBuilder.DropTable(
                name: "scrape_run_items");

            migrationBuilder.AlterColumn<string>(
                name: "credential_key",
                table: "forums",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
