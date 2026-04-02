using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
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
    private readonly IImdbStagingRepository _stagingRepo;
    private readonly ILogger<ImdbImportService> _logger;
    private const int ImdbSourceId = 1;
    
    public ImdbImportService(
        AppDbContext context,
        ImdbDatasetDownloader downloader,
        ImdbDatasetParser parser,
        IImdbStagingRepository stagingRepo,
        ILogger<ImdbImportService> logger)
    {
        _context = context;
        _downloader = downloader;
        _parser = parser;
        _stagingRepo = stagingRepo;
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
        // Import title.basics
        _logger.LogInformation("Importing title.basics to staging");
        long rowCount = 0;
        await foreach (var chunk in _parser.ParseTitleBasicsAsync(basicsPath))
        {
            await _stagingRepo.BulkInsertBasicsAsync(chunk, cancellationToken);
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
            await _stagingRepo.BulkInsertAkasAsync(chunk, cancellationToken);
        }
        
        // Import title.episode
        _logger.LogInformation("Importing title.episode to staging");
        await foreach (var chunk in _parser.ParseTitleEpisodeAsync(episodePath))
        {
            await _stagingRepo.BulkInsertEpisodeAsync(chunk, cancellationToken);
        }
        
        // Import title.ratings
        _logger.LogInformation("Importing title.ratings to staging");
        await foreach (var chunk in _parser.ParseTitleRatingsAsync(ratingsPath))
        {
            await _stagingRepo.BulkInsertRatingsAsync(chunk, cancellationToken);
        }
    }
    
    protected virtual async Task UpsertToLiveTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Upserting from staging to live tables");
        await _stagingRepo.UpsertToLiveTablesAsync(cancellationToken);
        _logger.LogInformation("Upsert completed");
    }
    
    protected virtual async Task CleanupStagingTablesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truncating staging tables");
        await _stagingRepo.CleanupStagingTablesAsync(cancellationToken);
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
