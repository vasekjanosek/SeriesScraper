using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class MetadataSearchResultTests
{
    [Fact]
    public void MetadataSearchResult_NewTitle_HasNullMediaId()
    {
        var result = new MetadataSearchResult
        {
            CanonicalTitle = "Breaking Bad",
            Type = "series",
            ConfidenceScore = 0.95m,
            ExternalId = "tt0903747"
        };

        result.MediaId.Should().BeNull();
        result.Year.Should().BeNull();
    }

    [Fact]
    public void MetadataSearchResult_ExistingTitle_HasMediaId()
    {
        var result = new MetadataSearchResult
        {
            MediaId = 42,
            CanonicalTitle = "The Matrix",
            Year = 1999,
            Type = "movie",
            ConfidenceScore = 1.0m,
            ExternalId = "tt0133093"
        };

        result.MediaId.Should().Be(42);
        result.Year.Should().Be(1999);
        result.ConfidenceScore.Should().Be(1.0m);
    }

    [Fact]
    public void MetadataSearchResult_WithSameValues_AreEqual()
    {
        var a = new MetadataSearchResult
        {
            CanonicalTitle = "Test",
            Type = "movie",
            ConfidenceScore = 0.5m,
            ExternalId = "tt1234"
        };
        var b = new MetadataSearchResult
        {
            CanonicalTitle = "Test",
            Type = "movie",
            ConfidenceScore = 0.5m,
            ExternalId = "tt1234"
        };

        a.Should().Be(b);
    }
}
