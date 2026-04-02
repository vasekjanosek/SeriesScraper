using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbDatasetParserTests
{
    [Fact]
    public async Task ParseTitleBasicsAsync_ValidData_ReturnsEntities()
    {
        // Arrange
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tThe Matrix\t\\N\t0\t1999\t\\N\t136\tAction,Sci-Fi",
            "tt0000002\ttvSeries\tBreaking Bad\t\\N\t0\t2008\t2013\t\\N\tCrime,Drama"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleBasicsStaging>>();
        await foreach (var chunk in parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        var allEntities = chunks.SelectMany(c => c).ToList();
        allEntities.Should().HaveCount(2);
        
        var matrix = allEntities[0];
        matrix.Tconst.Should().Be("tt0000001");
        matrix.TitleType.Should().Be("movie");
        matrix.PrimaryTitle.Should().Be("The Matrix");
        matrix.StartYear.Should().Be(1999);
        matrix.Genres.Should().Be("Action,Sci-Fi");
        
        var breakingBad = allEntities[1];
        breakingBad.Tconst.Should().Be("tt0000002");
        breakingBad.EndYear.Should().Be(2013);
        
        // Cleanup
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleBasicsAsync_HandlesNullValues()
    {
        // Arrange
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tTest\t\\N\t0\t\\N\t\\N\t\\N\t\\N"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleBasicsStaging>>();
        await foreach (var chunk in parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        var entity = chunks.SelectMany(c => c).Single();
        entity.OriginalTitle.Should().BeNull();
        entity.StartYear.Should().BeNull();
        entity.EndYear.Should().BeNull();
        entity.RuntimeMinutes.Should().BeNull();
        entity.Genres.Should().BeNull();
        
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleBasicsAsync_MalformedRow_LogsAndContinues()
    {
        // Arrange
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tValid Title 1\t\\N\t0\t1999\t\\N\t136\tAction",
            "MALFORMED\tONLY_TWO_FIELDS", // This row should be skipped
            "tt0000003\tmovie\tValid Title 2\t\\N\t0\t2020\t\\N\t120\tDrama"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleBasicsStaging>>();
        await foreach (var chunk in parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert - Malformed row should be skipped, but valid rows should be parsed
        var allEntities = chunks.SelectMany(c => c).ToList();
        allEntities.Should().HaveCount(2);
        allEntities[0].Tconst.Should().Be("tt0000001");
        allEntities[1].Tconst.Should().Be("tt0000003");
        
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleAkasAsync_ValidData_ReturnsEntities()
    {
        // Arrange
        var gzipPath = CreateTitleAkasFile(new[]
        {
            "tt0000001\t1\tThe Matrix\tUS\ten\toriginal\t\\N\t1",
            "tt0000001\t2\tMatrix\tCZ\tcs\t\\N\t\\N\t0"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleAkasStaging>>();
        await foreach (var chunk in parser.ParseTitleAkasAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        var allEntities = chunks.SelectMany(c => c).ToList();
        allEntities.Should().HaveCount(2);
        
        var alias1 = allEntities[0];
        alias1.Tconst.Should().Be("tt0000001");
        alias1.Ordering.Should().Be(1);
        alias1.Title.Should().Be("The Matrix");
        alias1.Region.Should().Be("US");
        alias1.Language.Should().Be("en");
        alias1.IsOriginalTitle.Should().BeTrue();
        
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleEpisodeAsync_ValidData_ReturnsEntities()
    {
        // Arrange
        var gzipPath = CreateTitleEpisodeFile(new[]
        {
            "tt0001000\ttt0000001\t1\t5",
            "tt0001001\ttt0000001\t2\t3"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleEpisodeStaging>>();
        await foreach (var chunk in parser.ParseTitleEpisodeAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        var allEntities = chunks.SelectMany(c => c).ToList();
        allEntities.Should().HaveCount(2);
        
        var episode1 = allEntities[0];
        episode1.Tconst.Should().Be("tt0001000");
        episode1.ParentTconst.Should().Be("tt0000001");
        episode1.SeasonNumber.Should().Be(1);
        episode1.EpisodeNumber.Should().Be(5);
        
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleRatingsAsync_ValidData_ReturnsEntities()
    {
        // Arrange
        var gzipPath = CreateTitleRatingsFile(new[]
        {
            "tt0000001\t8.7\t1500000",
            "tt0000002\t9.5\t2000000"
        });
        
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleRatingsStaging>>();
        await foreach (var chunk in parser.ParseTitleRatingsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        var allEntities = chunks.SelectMany(c => c).ToList();
        allEntities.Should().HaveCount(2);
        
        var rating1 = allEntities[0];
        rating1.Tconst.Should().Be("tt0000001");
        rating1.AverageRating.Should().Be(8.7m);
        rating1.NumVotes.Should().Be(1500000);
        
        File.Delete(gzipPath);
    }
    
    [Fact]
    public async Task ParseTitleBasicsAsync_ChunksDataCorrectly()
    {
        // Arrange
        var rows = Enumerable.Range(1, 150)
            .Select(i => $"tt{i:D7}\tmovie\tTitle {i}\t\\N\t0\t2020\t\\N\t120\tAction")
            .ToArray();
        
        var gzipPath = CreateTitleBasicsFile(rows);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        
        // Act
        var chunks = new List<List<ImdbTitleBasicsStaging>>();
        await foreach (var chunk in parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 50))
        {
            chunks.Add(chunk);
        }
        
        // Assert
        chunks.Should().HaveCount(3); // 150 rows / 50 chunk size = 3 chunks
        chunks[0].Should().HaveCount(50);
        chunks[1].Should().HaveCount(50);
        chunks[2].Should().HaveCount(50);
        
        File.Delete(gzipPath);
    }
    
    private static string CreateTitleBasicsFile(string[] dataRows)
    {
        return CreateGzipFile(
            "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres",
            dataRows);
    }
    
    private static string CreateTitleAkasFile(string[] dataRows)
    {
        return CreateGzipFile(
            "titleId\tordering\ttitle\tregion\tlanguage\ttypes\tattributes\tisOriginalTitle",
            dataRows);
    }
    
    private static string CreateTitleEpisodeFile(string[] dataRows)
    {
        return CreateGzipFile(
            "tconst\tparentTconst\tseasonNumber\tepisodeNumber",
            dataRows);
    }
    
    private static string CreateTitleRatingsFile(string[] dataRows)
    {
        return CreateGzipFile(
            "tconst\taverageRating\tnumVotes",
            dataRows);
    }
    
    private static string CreateGzipFile(string header, string[] dataRows)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.tsv.gz");
        
        using var fileStream = File.Create(tempPath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest);
        using var writer = new StreamWriter(gzipStream);
        
        writer.WriteLine(header);
        foreach (var row in dataRows)
        {
            writer.WriteLine(row);
        }
        
        return tempPath;
    }
}
