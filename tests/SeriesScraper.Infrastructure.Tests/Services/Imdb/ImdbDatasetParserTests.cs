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
    
    // ===== Edge-case / error-path tests for ≥90% coverage =====

    [Fact]
    public async Task ParseTitleBasicsAsync_IsAdultTrue_ParsesCorrectly()
    {
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tadult\tAdult Film\tOriginal Title\t1\t2020\t\\N\t90\tAdult"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10));

        entities.Should().ContainSingle();
        entities[0].IsAdult.Should().BeTrue();
        entities[0].OriginalTitle.Should().Be("Original Title");

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleBasicsAsync_EmptyFile_ReturnsNoChunks()
    {
        var gzipPath = CreateTitleBasicsFile(Array.Empty<string>());
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleBasicsStaging>>();
        await foreach (var chunk in parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleBasicsAsync_WhitespaceIntField_ReturnsNull()
    {
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tTest\t\\N\t0\t \t\\N\t\\N\t\\N"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10));

        entities.Should().ContainSingle();
        entities[0].StartYear.Should().BeNull();

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleBasicsAsync_LongMalformedRow_TruncatesInLog()
    {
        // Row > 200 chars to trigger TruncateRow
        var longRow = new string('X', 300);
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tValid\t\\N\t0\t2020\t\\N\t120\tAction",
            longRow, // malformed and long
            "tt0000003\tmovie\tValid2\t\\N\t0\t2021\t\\N\t130\tDrama"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleAkasAsync_MalformedRow_LogsAndContinues()
    {
        var gzipPath = CreateTitleAkasFile(new[]
        {
            "tt0000001\t1\tThe Matrix\tUS\ten\toriginal\t\\N\t1",
            "MALFORMED\tONLY_TWO",
            "tt0000003\t2\tMatrix\tCZ\tcs\t\\N\t\\N\t0"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleAkasAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);
        entities[0].Tconst.Should().Be("tt0000001");
        entities[1].Tconst.Should().Be("tt0000003");

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleAkasAsync_NullOptionalFields_ParsesCorrectly()
    {
        var gzipPath = CreateTitleAkasFile(new[]
        {
            "tt0000001\t1\tTitle\t\\N\t\\N\t\\N\t\\N\t0"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleAkasAsync(gzipPath, chunkSize: 10));

        entities.Should().ContainSingle();
        var e = entities[0];
        e.Region.Should().BeNull();
        e.Language.Should().BeNull();
        e.Types.Should().BeNull();
        e.Attributes.Should().BeNull();
        e.IsOriginalTitle.Should().BeFalse();

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleAkasAsync_EmptyFile_ReturnsNoChunks()
    {
        var gzipPath = CreateTitleAkasFile(Array.Empty<string>());
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleAkasStaging>>();
        await foreach (var chunk in parser.ParseTitleAkasAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleAkasAsync_ChunksDataCorrectly()
    {
        var rows = Enumerable.Range(1, 75)
            .Select(i => $"tt{i:D7}\t{i}\tTitle {i}\tUS\ten\t\\N\t\\N\t1")
            .ToArray();

        var gzipPath = CreateTitleAkasFile(rows);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleAkasStaging>>();
        await foreach (var chunk in parser.ParseTitleAkasAsync(gzipPath, chunkSize: 25))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3);
        chunks[0].Should().HaveCount(25);
        chunks[1].Should().HaveCount(25);
        chunks[2].Should().HaveCount(25);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleEpisodeAsync_MalformedRow_LogsAndContinues()
    {
        var gzipPath = CreateTitleEpisodeFile(new[]
        {
            "tt0001000\ttt0000001\t1\t5",
            "BAD_ROW",
            "tt0001002\ttt0000001\t2\t3"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleEpisodeAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);
        entities[0].Tconst.Should().Be("tt0001000");
        entities[1].Tconst.Should().Be("tt0001002");

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleEpisodeAsync_NullSeasonAndEpisode_ParsesCorrectly()
    {
        var gzipPath = CreateTitleEpisodeFile(new[]
        {
            "tt0001000\ttt0000001\t\\N\t\\N"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleEpisodeAsync(gzipPath, chunkSize: 10));

        entities.Should().ContainSingle();
        entities[0].SeasonNumber.Should().BeNull();
        entities[0].EpisodeNumber.Should().BeNull();

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleEpisodeAsync_EmptyFile_ReturnsNoChunks()
    {
        var gzipPath = CreateTitleEpisodeFile(Array.Empty<string>());
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleEpisodeStaging>>();
        await foreach (var chunk in parser.ParseTitleEpisodeAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleEpisodeAsync_ChunksDataCorrectly()
    {
        var rows = Enumerable.Range(1, 60)
            .Select(i => $"tt{i:D7}\ttt0000001\t{i}\t{i}")
            .ToArray();

        var gzipPath = CreateTitleEpisodeFile(rows);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleEpisodeStaging>>();
        await foreach (var chunk in parser.ParseTitleEpisodeAsync(gzipPath, chunkSize: 20))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3);
        chunks[0].Should().HaveCount(20);
        chunks[1].Should().HaveCount(20);
        chunks[2].Should().HaveCount(20);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleRatingsAsync_MalformedRow_LogsAndContinues()
    {
        var gzipPath = CreateTitleRatingsFile(new[]
        {
            "tt0000001\t8.7\t1500000",
            "MALFORMED",
            "tt0000003\t9.1\t2000000"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleRatingsAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);
        entities[0].Tconst.Should().Be("tt0000001");
        entities[1].Tconst.Should().Be("tt0000003");

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleRatingsAsync_InvalidDecimal_SkipsRow()
    {
        var gzipPath = CreateTitleRatingsFile(new[]
        {
            "tt0000001\t8.7\t1500000",
            "tt0000002\tNOT_A_NUMBER\t1000",
            "tt0000003\t9.1\t2000000"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleRatingsAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleRatingsAsync_EmptyFile_ReturnsNoChunks()
    {
        var gzipPath = CreateTitleRatingsFile(Array.Empty<string>());
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleRatingsStaging>>();
        await foreach (var chunk in parser.ParseTitleRatingsAsync(gzipPath, chunkSize: 10))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleRatingsAsync_ChunksDataCorrectly()
    {
        var rows = Enumerable.Range(1, 40)
            .Select(i => $"tt{i:D7}\t{5.0 + i * 0.1:F1}\t{i * 1000}")
            .ToArray();

        var gzipPath = CreateTitleRatingsFile(rows);
        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var chunks = new List<List<ImdbTitleRatingsStaging>>();
        await foreach (var chunk in parser.ParseTitleRatingsAsync(gzipPath, chunkSize: 15))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3); // 15 + 15 + 10
        chunks[0].Should().HaveCount(15);
        chunks[1].Should().HaveCount(15);
        chunks[2].Should().HaveCount(10);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleAkasAsync_InvalidOrdering_SkipsRow()
    {
        var gzipPath = CreateTitleAkasFile(new[]
        {
            "tt0000001\t1\tTitle1\tUS\ten\t\\N\t\\N\t1",
            "tt0000002\tNOT_INT\tTitle2\tUS\ten\t\\N\t\\N\t0",
            "tt0000003\t3\tTitle3\tUS\ten\t\\N\t\\N\t1"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleAkasAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);

        File.Delete(gzipPath);
    }

    [Fact]
    public async Task ParseTitleBasicsAsync_InvalidIntInYear_SkipsRow()
    {
        var gzipPath = CreateTitleBasicsFile(new[]
        {
            "tt0000001\tmovie\tValid\t\\N\t0\t2020\t\\N\t120\tAction",
            "tt0000002\tmovie\tBad Year\t\\N\t0\tNOT_INT\t\\N\t120\tDrama",
            "tt0000003\tmovie\tValid2\t\\N\t0\t2021\t\\N\t130\tComedy"
        });

        var parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
        var entities = await CollectAsync(parser.ParseTitleBasicsAsync(gzipPath, chunkSize: 10));

        entities.Should().HaveCount(2);

        File.Delete(gzipPath);
    }

    // ===== Helpers =====

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<List<T>> source)
    {
        var all = new List<T>();
        await foreach (var chunk in source)
        {
            all.AddRange(chunk);
        }
        return all;
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
