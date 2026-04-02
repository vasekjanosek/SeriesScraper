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
    public void MetadataSearchResult_StoresAllProperties()
    {
        var result = new MetadataSearchResult
        {
            MediaId = 10,
            CanonicalTitle = "Test Title",
            Year = 2020,
            Type = "series",
            ConfidenceScore = 0.75m,
            ExternalId = "ext-123"
        };

        result.MediaId.Should().Be(10);
        result.CanonicalTitle.Should().Be("Test Title");
        result.Year.Should().Be(2020);
        result.Type.Should().Be("series");
        result.ConfidenceScore.Should().Be(0.75m);
        result.ExternalId.Should().Be("ext-123");
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
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentCanonicalTitle_AreNotEqual()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "A", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "B", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentType_AreNotEqual()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "X", Type = "series", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentConfidenceScore_AreNotEqual()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.9m, ExternalId = "tt1" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentExternalId_AreNotEqual()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt2" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentMediaId_AreNotEqual()
    {
        var a = new MetadataSearchResult { MediaId = 1, CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { MediaId = 2, CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void MetadataSearchResult_WithDifferentYear_AreNotEqual()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "X", Year = 2000, Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "X", Year = 2010, Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void MetadataSearchResult_GetHashCode_SameForEqualInstances()
    {
        var a = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var b = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void MetadataSearchResult_ToString_ContainsTypeName()
    {
        var result = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };

        result.ToString().Should().Contain("MetadataSearchResult");
    }

    [Fact]
    public void MetadataSearchResult_WithExpression_CreatesModifiedCopy()
    {
        var original = new MetadataSearchResult { CanonicalTitle = "X", Type = "movie", ConfidenceScore = 0.5m, ExternalId = "tt1" };
        var modified = original with { Year = 2025, MediaId = 99 };

        modified.Year.Should().Be(2025);
        modified.MediaId.Should().Be(99);
        original.Year.Should().BeNull();
        original.MediaId.Should().BeNull();
    }
}
