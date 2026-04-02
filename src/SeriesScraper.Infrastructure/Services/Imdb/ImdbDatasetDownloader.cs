using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace SeriesScraper.Infrastructure.Services.Imdb;

/// <summary>
/// Downloads IMDB TSV.gz datasets from datasets.imdbapi.dev.
/// Validates download integrity before returning the file path.
/// AC#7 from issue #22.
/// </summary>
public class ImdbDatasetDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImdbDatasetDownloader> _logger;
    private const string BaseUrl = "https://datasets.imdbapi.com";
    
    // Minimum row count thresholds for validation
    private const int MinRowsBasics = 10_000_000;   // title.basics has ~10M+ rows
    private const int MinRowsAkas = 30_000_000;     // title.akas has ~30M+ rows  
    private const int MinRowsEpisode = 7_000_000;   // title.episode has ~7M+ rows
    private const int MinRowsRatings = 1_000_000;   // title.ratings has ~1M+ rows
    
    public ImdbDatasetDownloader(HttpClient httpClient, ILogger<ImdbDatasetDownloader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    /// <summary>
    /// Downloads a dataset file to a temporary location.
    /// </summary>
    /// <param name="datasetName">Dataset filename (e.g., "title.basics.tsv.gz")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the downloaded temp file</returns>
    /// <exception cref="InvalidDataException">Thrown if download validation fails</exception>
    public virtual async Task<string> DownloadDatasetAsync(string datasetName, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/{datasetName}";
        _logger.LogInformation("Starting download of {DatasetName} from {Url}", datasetName, url);
        
        var tempPath = Path.Combine(Path.GetTempPath(), $"imdb_{Guid.NewGuid():N}_{datasetName}");
        
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            await using (var fileStream = File.Create(tempPath))
            {
                await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await networkStream.CopyToAsync(fileStream, cancellationToken);
            } // File stream is disposed here, file is now closed
            
            _logger.LogInformation("Downloaded {DatasetName} to {TempPath} ({SizeBytes} bytes)", 
                datasetName, tempPath, new FileInfo(tempPath).Length);
            
            // Validate the download
            ValidateDownload(tempPath, datasetName);
            
            return tempPath;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            _logger.LogError(ex, "Failed to download {DatasetName}", datasetName);
            
            // Clean up temp file on error
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            
            throw;
        }
    }
    
    /// <summary>
    /// Validates the downloaded file:
    /// - Verifies gzip header
    /// - Checks minimum row count threshold
    /// AC#7 from issue #22.
    /// </summary>
    private void ValidateDownload(string filePath, string datasetName)
    {
        _logger.LogInformation("Validating download: {DatasetName}", datasetName);
        
        // Check gzip header
        using (var fs = File.OpenRead(filePath))
        {
            var header = new byte[2];
            if (fs.Read(header, 0, 2) != 2 || header[0] != 0x1f || header[1] != 0x8b)
            {
                throw new InvalidDataException($"Invalid gzip header in {datasetName}");
            }
        }
        
        // Count rows after decompression
        var rowCount = CountRows(filePath);
        var minRows = GetMinimumRowCount(datasetName);
        
        if (rowCount < minRows)
        {
            throw new InvalidDataException(
                $"Downloaded {datasetName} has only {rowCount} rows, expected at least {minRows}. " +
                "File may be truncated or corrupted.");
        }
        
        _logger.LogInformation("Validation passed: {DatasetName} has {RowCount} rows (min: {MinRows})", 
            datasetName, rowCount, minRows);
    }
    
    private int CountRows(string gzipPath)
    {
        using var fileStream = File.OpenRead(gzipPath);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        
        var count = 0;
        while (reader.ReadLine() != null)
        {
            count++;
        }
        
        // Subtract 1 for header row
        return Math.Max(0, count - 1);
    }
    
    private int GetMinimumRowCount(string datasetName)
    {
        return datasetName switch
        {
            "title.basics.tsv.gz" => MinRowsBasics,
            "title.akas.tsv.gz" => MinRowsAkas,
            "title.episode.tsv.gz" => MinRowsEpisode,
            "title.ratings.tsv.gz" => MinRowsRatings,
            _ => 1000 // Default minimum for unknown datasets
        };
    }
}
