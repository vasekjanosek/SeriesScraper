using System.Globalization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Services.Imdb;

/// <summary>
/// Parses IMDB TSV datasets with chunked streaming to enforce memory ceiling.
/// AC#5, AC#9 from issue #22.
/// </summary>
public class ImdbDatasetParser
{
    private readonly ILogger<ImdbDatasetParser> _logger;
    private const int DefaultChunkSize = 50_000; // ~256 MB at 5 KB/row
    
    public ImdbDatasetParser(ILogger<ImdbDatasetParser> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Parses title.basics.tsv.gz into chunks of staging entities.
    /// </summary>
    public virtual async IAsyncEnumerable<List<ImdbTitleBasicsStaging>> ParseTitleBasicsAsync(
        string gzipPath,
        int chunkSize = DefaultChunkSize)
    {
        await using var fileStream = File.OpenRead(gzipPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        
        // Skip header row
        await reader.ReadLineAsync();
        
        var chunk = new List<ImdbTitleBasicsStaging>(chunkSize);
        var rowNumber = 1; // Start at 1 (after header)
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            
            ImdbTitleBasicsStaging? entity = null;
            try
            {
                entity = ParseTitleBasicsRow(line);
                chunk.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Malformed row at line {RowNumber}: {Error}. Row: {RowContent}", 
                    rowNumber, ex.Message, TruncateRow(line));
                // Continue past malformed rows per AC#9
            }
                
            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<ImdbTitleBasicsStaging>(chunkSize);
            }
        }
        
        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }
    
    /// <summary>
    /// Parses title.akas.tsv.gz into chunks of staging entities.
    /// </summary>
    public virtual async IAsyncEnumerable<List<ImdbTitleAkasStaging>> ParseTitleAkasAsync(
        string gzipPath,
        int chunkSize = DefaultChunkSize)
    {
        await using var fileStream = File.OpenRead(gzipPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        
        await reader.ReadLineAsync(); // Skip header
        
        var chunk = new List<ImdbTitleAkasStaging>(chunkSize);
        var rowNumber = 1;
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            
            try
            {
                var entity = ParseTitleAkasRow(line);
                chunk.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Malformed row at line {RowNumber}: {Error}. Row: {RowContent}", 
                    rowNumber, ex.Message, TruncateRow(line));
            }
            
            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<ImdbTitleAkasStaging>(chunkSize);
            }
        }
        
        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }
    
    /// <summary>
    /// Parses title.episode.tsv.gz into chunks of staging entities.
    /// </summary>
    public virtual async IAsyncEnumerable<List<ImdbTitleEpisodeStaging>> ParseTitleEpisodeAsync(
        string gzipPath,
        int chunkSize = DefaultChunkSize)
    {
        await using var fileStream = File.OpenRead(gzipPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        
        await reader.ReadLineAsync(); // Skip header
        
        var chunk = new List<ImdbTitleEpisodeStaging>(chunkSize);
        var rowNumber = 1;
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            
            try
            {
                var entity = ParseTitleEpisodeRow(line);
                chunk.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Malformed row at line {RowNumber}: {Error}. Row: {RowContent}", 
                    rowNumber, ex.Message, TruncateRow(line));
            }
            
            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<ImdbTitleEpisodeStaging>(chunkSize);
            }
        }
        
        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }
    
    /// <summary>
    /// Parses title.ratings.tsv.gz into chunks of staging entities.
    /// </summary>
    public virtual async IAsyncEnumerable<List<ImdbTitleRatingsStaging>> ParseTitleRatingsAsync(
        string gzipPath,
        int chunkSize = DefaultChunkSize)
    {
        await using var fileStream = File.OpenRead(gzipPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        
        await reader.ReadLineAsync(); // Skip header
        
        var chunk = new List<ImdbTitleRatingsStaging>(chunkSize);
        var rowNumber = 1;
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            
            try
            {
                var entity = ParseTitleRatingsRow(line);
                chunk.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Malformed row at line {RowNumber}: {Error}. Row: {RowContent}", 
                    rowNumber, ex.Message, TruncateRow(line));
            }
            
            if (chunk.Count >= chunkSize)
            {
                yield return chunk;
                chunk = new List<ImdbTitleRatingsStaging>(chunkSize);
            }
        }
        
        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }
    
    private ImdbTitleBasicsStaging ParseTitleBasicsRow(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 9)
        {
            throw new FormatException($"Expected 9 fields, got {fields.Length}");
        }
        
        return new ImdbTitleBasicsStaging
        {
            Tconst = fields[0],
            TitleType = fields[1],
            PrimaryTitle = fields[2],
            OriginalTitle = fields[3] == "\\N" ? null : fields[3],
            IsAdult = fields[4] == "1",
            StartYear = ParseNullableInt(fields[5]),
            EndYear = ParseNullableInt(fields[6]),
            RuntimeMinutes = ParseNullableInt(fields[7]),
            Genres = fields[8] == "\\N" ? null : fields[8]
        };
    }
    
    private ImdbTitleAkasStaging ParseTitleAkasRow(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 8)
        {
            throw new FormatException($"Expected 8 fields, got {fields.Length}");
        }
        
        return new ImdbTitleAkasStaging
        {
            Tconst = fields[0],
            Ordering = int.Parse(fields[1], CultureInfo.InvariantCulture),
            Title = fields[2],
            Region = fields[3] == "\\N" ? null : fields[3],
            Language = fields[4] == "\\N" ? null : fields[4],
            Types = fields[5] == "\\N" ? null : fields[5],
            Attributes = fields[6] == "\\N" ? null : fields[6],
            IsOriginalTitle = fields[7] == "1"
        };
    }
    
    private ImdbTitleEpisodeStaging ParseTitleEpisodeRow(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 4)
        {
            throw new FormatException($"Expected 4 fields, got {fields.Length}");
        }
        
        return new ImdbTitleEpisodeStaging
        {
            Tconst = fields[0],
            ParentTconst = fields[1],
            SeasonNumber = ParseNullableInt(fields[2]),
            EpisodeNumber = ParseNullableInt(fields[3])
        };
    }
    
    private ImdbTitleRatingsStaging ParseTitleRatingsRow(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 3)
        {
            throw new FormatException($"Expected 3 fields, got {fields.Length}");
        }
        
        return new ImdbTitleRatingsStaging
        {
            Tconst = fields[0],
            AverageRating = decimal.Parse(fields[1], CultureInfo.InvariantCulture),
            NumVotes = int.Parse(fields[2], CultureInfo.InvariantCulture)
        };
    }
    
    private int? ParseNullableInt(string value)
    {
        if (value == "\\N" || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        
        return int.Parse(value, CultureInfo.InvariantCulture);
    }
    
    private string TruncateRow(string row)
    {
        const int maxLength = 200;
        return row.Length <= maxLength ? row : row.Substring(0, maxLength) + "...";
    }
}
