using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Services.Imdb;

/// <summary>
/// IMDB dataset import service.
/// Implements bulk import with staging tables and Npgsql COPY protocol.
/// AC#1-11 from issue #22.
/// 
/// Information from information currently available on https://www.imdb.com is licensed
/// for non-commercial use only under the terms of the IMDB Conditions of Use.
/// See: https://www.imdb.com/conditions
/// This application is intended for personal, non-commercial use only.
/// </summary>
public class ImdbImportService
{
    private readonly AppDbContext _context;
    private readonly ImdbDatasetDownloader _downloader;
    private readonly ImdbDatasetParser _parser;
    private readonly ILogger<ImdbImportService> _logger;
    private const int ImdbSourceId = 1;
    
    public ImdbImportService(
        AppDbContext context,
        ImdbDatasetDownloader downloader,
        ImdbDatasetParser parser,
        ILogger<ImdbImportService> logger)
    {
        _context = context;
        _downloader = downloader;
        _parser = parser;
        _logger = logger;
    }
    
    /// <summary>
    /// Runs the full IMDB import pipeline.
    /// Returns the import run ID for tracking.
    /// </summary>
    public virtual async Task<int> RunImportAsync(CancellationToken cancellationToken)
    {
        var importRun = new DataSourceImportRun
        {
            SourceId = ImdbSourceId,
            StartedAt = DateTime.UtcNow,
            Status = ImportRunStatus.Running.ToString(),
            RowsImported = 0
        };
        
        _context.DataSourceImportRuns.Add(importRun);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Started IMDB import run {ImportRunId}", importRun.ImportRunId);
        
        try
        {
            // Download all datasets
            var basicsPath = await _downloader.DownloadDatasetAsync("title.basics.tsv.gz", cancellationToken);
            var akasPath = await _downloader.DownloadDatasetAsync("title.akas.tsv.gz", cancellationToken);
            var episodePath = await _downloader.DownloadDatasetAsync("title.episode.tsv.gz", cancellationToken);
            var ratingsPath = await _downloader.DownloadDatasetAsync("title.ratings.tsv.gz", cancellationToken);
            
            try
            {
                // Import to staging tables
                await ImportToStagingTablesAsync(basicsPath, akasPath, episodePath, ratingsPath, importRun, cancellationToken);
                
                // Upsert from staging to live tables
                await UpsertToLiveTablesAsync(cancellationToken);
                
                // Clean up staging tables
                await CleanupStagingTablesAsync(cancellationToken);
                
                // Mark as complete
                importRun.Status = ImportRunStatus.Complete.ToString();
                importRun.FinishedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Completed IMDB import run {ImportRunId} ({RowsImported} rows)", 
                    importRun.ImportRunId, importRun.RowsImported);
            }
            finally
            {
                // Clean up downloaded files
                CleanupTempFiles(basicsPath, akasPath, episodePath, ratingsPath);
            }
            
            return importRun.ImportRunId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMDB import run {ImportRunId} failed", importRun.ImportRunId);
            
            importRun.Status = ImportRunStatus.Failed.ToString();
            importRun.FinishedAt = DateTime.UtcNow;
            importRun.ErrorMessage = ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            
            throw;
        }
    }
    
    protected virtual async Task ImportToStagingTablesAsync(
        string basicsPath,
        string akasPath,
        string episodePath,
        string ratingsPath,
        DataSourceImportRun importRun,
        CancellationToken cancellationToken)
    {
        var connectionString = _context.Database.GetConnectionString() 
            ?? throw new InvalidOperationException("Connection string not configured");
        
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        
        // Import title.basics
        _logger.LogInformation("Importing title.basics to staging");
        long rowCount = 0;
        await foreach (var chunk in _parser.ParseTitleBasicsAsync(basicsPath))
        {
            await BulkInsertBasicsAsync(connection, chunk, cancellationToken);
            rowCount += chunk.Count;
            
            // Update progress periodically
            if (rowCount % 500_000 == 0)
            {
                importRun.RowsImported = rowCount;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Imported {RowCount} rows from title.basics", rowCount);
            }
        }
        importRun.RowsImported = rowCount;
        await _context.SaveChangesAsync(cancellationToken);
        
        // Import title.akas
        _logger.LogInformation("Importing title.akas to staging");
        await foreach (var chunk in _parser.ParseTitleAkasAsync(akasPath))
        {
            await BulkInsertAkasAsync(connection, chunk, cancellationToken);
        }
        
        // Import title.episode
        _logger.LogInformation("Importing title.episode to staging");
        await foreach (var chunk in _parser.ParseTitleEpisodeAsync(episodePath))
        {
            await BulkInsertEpisodeAsync(connection, chunk, cancellationToken);
        }
        
        // Import title.ratings
        _logger.LogInformation("Importing title.ratings to staging");
        await foreach (var chunk in _parser.ParseTitleRatingsAsync(ratingsPath))
        {
            await BulkInsertRatingsAsync(connection, chunk, cancellationToken);
        }
    }
    
    private async Task BulkInsertBasicsAsync(
        NpgsqlConnection connection,
        List<ImdbTitleBasicsStaging> entities,
        CancellationToken cancellationToken)
    {
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
    
    private async Task BulkInsertAkasAsync(
        NpgsqlConnection connection,
        List<ImdbTitleAkasStaging> entities,
        CancellationToken cancellationToken)
    {
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
    
    private async Task BulkInsertEpisodeAsync(
        NpgsqlConnection connection,
        List<ImdbTitleEpisodeStaging> entities,
        CancellationToken cancellationToken)
    {
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
    
    private async Task BulkInsertRatingsAsync(
        NpgsqlConnection connection,
        List<ImdbTitleRatingsStaging> entities,
        CancellationToken cancellationToken)
    {
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
    
    protected virtual async Task UpsertToLiveTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Upserting from staging to live tables");
        
        // Execute raw SQL for upsert operations
        // This is more efficient than EF Core for bulk operations
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
        
        _logger.LogInformation("Upsert completed");
    }
    
    protected virtual async Task CleanupStagingTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truncating staging tables");
        
        await _context.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE imdb_title_basics_staging;
            TRUNCATE TABLE imdb_title_akas_staging;
            TRUNCATE TABLE imdb_title_episode_staging;
            TRUNCATE TABLE imdb_title_ratings_staging;
        ", cancellationToken);
    }
    
    private void CleanupTempFiles(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogDebug("Deleted temp file: {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file: {Path}", path);
            }
        }
    }
}
