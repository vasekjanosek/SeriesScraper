using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Repositories;

/// <summary>
/// Repository for IMDB staging table operations using Npgsql COPY protocol and raw SQL.
/// Extracted from ImdbImportService for testability.
/// </summary>
public class ImdbStagingRepository : IImdbStagingRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ImdbStagingRepository> _logger;

    public ImdbStagingRepository(AppDbContext context, ILogger<ImdbStagingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task BulkInsertBasicsAsync(
        List<ImdbTitleBasicsStaging> entities,
        CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY imdb_title_basics_staging (tconst, title_type, primary_title, original_title, is_adult, start_year, end_year, runtime_minutes, genres) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var entity in entities)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(entity.Tconst, cancellationToken);
            await writer.WriteAsync(entity.TitleType, cancellationToken);
            await writer.WriteAsync(entity.PrimaryTitle, cancellationToken);
            await writer.WriteAsync(entity.OriginalTitle, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(entity.IsAdult, cancellationToken);
            await writer.WriteAsync(entity.StartYear, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(entity.EndYear, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(entity.RuntimeMinutes, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(entity.Genres, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    public async Task BulkInsertAkasAsync(
        List<ImdbTitleAkasStaging> entities,
        CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY imdb_title_akas_staging (tconst, ordering, title, region, language, types, attributes, is_original_title) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var entity in entities)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(entity.Tconst, cancellationToken);
            await writer.WriteAsync(entity.Ordering, cancellationToken);
            await writer.WriteAsync(entity.Title, cancellationToken);
            await writer.WriteAsync(entity.Region, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(entity.Language, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(entity.Types, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(entity.Attributes, NpgsqlTypes.NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(entity.IsOriginalTitle, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    public async Task BulkInsertEpisodeAsync(
        List<ImdbTitleEpisodeStaging> entities,
        CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY imdb_title_episode_staging (tconst, parent_tconst, season_number, episode_number) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var entity in entities)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(entity.Tconst, cancellationToken);
            await writer.WriteAsync(entity.ParentTconst, cancellationToken);
            await writer.WriteAsync(entity.SeasonNumber, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(entity.EpisodeNumber, NpgsqlTypes.NpgsqlDbType.Integer, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    public async Task BulkInsertRatingsAsync(
        List<ImdbTitleRatingsStaging> entities,
        CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY imdb_title_ratings_staging (tconst, average_rating, num_votes) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        foreach (var entity in entities)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(entity.Tconst, cancellationToken);
            await writer.WriteAsync(entity.AverageRating, cancellationToken);
            await writer.WriteAsync(entity.NumVotes, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    public async Task UpsertToLiveTablesAsync(CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlRawAsync(@"
            -- Upsert MediaTitles and ImdbTitleDetails from title.basics staging
            WITH basics AS (
                SELECT DISTINCT ON (tconst)
                    tconst,
                    primary_title,
                    start_year,
                    title_type,
                    genres
                FROM imdb_title_basics_staging
                WHERE title_type IN ('movie', 'tvSeries', 'tvMiniSeries', 'tvMovie')
            )
            INSERT INTO media_titles (canonical_title, year, type, source_id, created_at, updated_at)
            SELECT 
                b.primary_title,
                b.start_year,
                CASE 
                    WHEN b.title_type = 'movie' OR b.title_type = 'tvMovie' THEN 'Movie'
                    WHEN b.title_type = 'tvSeries' OR b.title_type = 'tvMiniSeries' THEN 'Series'
                    ELSE 'Unknown'
                END,
                1, -- IMDB source_id
                CURRENT_TIMESTAMP,
                CURRENT_TIMESTAMP
            FROM basics b
            ON CONFLICT (canonical_title, year, type, source_id) DO UPDATE
            SET updated_at = CURRENT_TIMESTAMP
            RETURNING media_id, canonical_title;
            
            -- Upsert ImdbTitleDetails
            INSERT INTO imdb_title_details (media_id, tconst, genre_string)
            SELECT 
                mt.media_id,
                b.tconst,
                b.genres
            FROM imdb_title_basics_staging b
            INNER JOIN media_titles mt ON mt.canonical_title = b.primary_title AND mt.year = b.start_year
            WHERE mt.source_id = 1
            ON CONFLICT (media_id) DO UPDATE
            SET genre_string = EXCLUDED.genre_string;
            
            -- Upsert MediaTitleAliases from title.akas staging
            INSERT INTO media_title_aliases (media_id, alias_title, language, region)
            SELECT DISTINCT
                mt.media_id,
                a.title,
                a.language,
                a.region
            FROM imdb_title_akas_staging a
            INNER JOIN imdb_title_details itd ON itd.tconst = a.tconst
            INNER JOIN media_titles mt ON mt.media_id = itd.media_id
            WHERE a.title IS NOT NULL AND a.title != ''
            ON CONFLICT DO NOTHING;
            
            -- Upsert MediaEpisodes from title.episode staging
            INSERT INTO media_episodes (media_id, season, episode_number)
            SELECT 
                mt.media_id,
                COALESCE(e.season_number, 0),
                COALESCE(e.episode_number, 0)
            FROM imdb_title_episode_staging e
            INNER JOIN imdb_title_details itd ON itd.tconst = e.tconst
            INNER JOIN media_titles mt ON mt.media_id = itd.media_id
            WHERE e.season_number IS NOT NULL AND e.episode_number IS NOT NULL
            ON CONFLICT DO NOTHING;
            
            -- Upsert MediaRatings from title.ratings staging
            INSERT INTO media_ratings (media_id, source_id, rating, vote_count)
            SELECT 
                mt.media_id,
                1, -- IMDB source_id
                r.average_rating,
                r.num_votes
            FROM imdb_title_ratings_staging r
            INNER JOIN imdb_title_details itd ON itd.tconst = r.tconst
            INNER JOIN media_titles mt ON mt.media_id = itd.media_id
            ON CONFLICT (media_id, source_id) DO UPDATE
            SET 
                rating = EXCLUDED.rating,
                vote_count = EXCLUDED.vote_count;
        ", cancellationToken);
    }

    public async Task CleanupStagingTablesAsync(CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE imdb_title_basics_staging;
            TRUNCATE TABLE imdb_title_akas_staging;
            TRUNCATE TABLE imdb_title_episode_staging;
            TRUNCATE TABLE imdb_title_ratings_staging;
        ", cancellationToken);
    }

    private string GetConnectionString()
    {
        return _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string not configured");
    }
}
