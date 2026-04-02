using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SeriesScraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalMediaEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "imdb_title_details",
                columns: table => new
                {
                    media_id = table.Column<int>(type: "integer", nullable: false),
                    tconst = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    genre_string = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_title_details", x => x.media_id);
                    table.ForeignKey(
                        name: "fk_imdb_title_details_media_titles_media_id",
                        column: x => x.media_id,
                        principalTable: "media_titles",
                        principalColumn: "media_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_episodes",
                columns: table => new
                {
                    episode_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    media_id = table.Column<int>(type: "integer", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    episode_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_episodes", x => x.episode_id);
                    table.ForeignKey(
                        name: "fk_media_episodes_media_titles_media_id",
                        column: x => x.media_id,
                        principalTable: "media_titles",
                        principalColumn: "media_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_ratings",
                columns: table => new
                {
                    media_id = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    vote_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_ratings", x => new { x.media_id, x.source_id });
                    table.ForeignKey(
                        name: "fk_media_ratings_data_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "data_sources",
                        principalColumn: "source_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_media_ratings_media_titles_media_id",
                        column: x => x.media_id,
                        principalTable: "media_titles",
                        principalColumn: "media_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_title_aliases",
                columns: table => new
                {
                    alias_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    media_id = table.Column<int>(type: "integer", nullable: false),
                    alias_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_title_aliases", x => x.alias_id);
                    table.ForeignKey(
                        name: "fk_media_title_aliases_media_titles_media_id",
                        column: x => x.media_id,
                        principalTable: "media_titles",
                        principalColumn: "media_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImdbTitleDetails_Tconst",
                table: "imdb_title_details",
                column: "tconst",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaEpisodes_MediaId_Season_Episode",
                table: "media_episodes",
                columns: new[] { "media_id", "season", "episode_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_ratings_source_id",
                table: "media_ratings",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_MediaTitleAliases_AliasTitle",
                table: "media_title_aliases",
                column: "alias_title");

            migrationBuilder.CreateIndex(
                name: "IX_MediaTitleAliases_MediaId",
                table: "media_title_aliases",
                column: "media_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imdb_title_details");

            migrationBuilder.DropTable(
                name: "media_episodes");

            migrationBuilder.DropTable(
                name: "media_ratings");

            migrationBuilder.DropTable(
                name: "media_title_aliases");
        }
    }
}
