using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ParsedLinkTests
{
    [Fact]
    public void ParsedLink_StoresRequiredProperties()
    {
        var link = new ParsedLink
        {
            Url = "https://download.example.com/file.zip",
            LinkTypeId = 1,
            Scheme = "https"
        };

        link.Url.Should().Be("https://download.example.com/file.zip");
        link.LinkTypeId.Should().Be(1);
        link.Scheme.Should().Be("https");
        link.ParsedSeason.Should().BeNull();
        link.ParsedEpisode.Should().BeNull();
    }

    [Fact]
    public void ParsedLink_WithSeasonAndEpisode_StoresValues()
    {
        var link = new ParsedLink
        {
            Url = "https://example.com/show-s03e07",
            LinkTypeId = 2,
            Scheme = "https",
            ParsedSeason = 3,
            ParsedEpisode = 7
        };

        link.ParsedSeason.Should().Be(3);
        link.ParsedEpisode.Should().Be(7);
    }

    [Fact]
    public void ParsedLink_WithSameValues_AreEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ParsedLink_WithDifferentUrl_AreNotEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/a", LinkTypeId = 1, Scheme = "https" };
        var b = new ParsedLink { Url = "https://x.com/b", LinkTypeId = 1, Scheme = "https" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ParsedLink_WithDifferentLinkTypeId_AreNotEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 2, Scheme = "https" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ParsedLink_WithDifferentScheme_AreNotEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "magnet" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ParsedLink_WithDifferentParsedSeason_AreNotEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedSeason = 1 };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedSeason = 2 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ParsedLink_WithDifferentParsedEpisode_AreNotEqual()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedEpisode = 1 };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedEpisode = 2 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ParsedLink_GetHashCode_SameForEqualInstances()
    {
        var a = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedSeason = 3, ParsedEpisode = 7 };
        var b = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https", ParsedSeason = 3, ParsedEpisode = 7 };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ParsedLink_ToString_ContainsTypeName()
    {
        var link = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };

        link.ToString().Should().Contain("ParsedLink");
    }

    [Fact]
    public void ParsedLink_WithExpression_CreatesModifiedCopy()
    {
        var original = new ParsedLink { Url = "https://x.com/f", LinkTypeId = 1, Scheme = "https" };
        var modified = original with { ParsedSeason = 5, ParsedEpisode = 10 };

        modified.ParsedSeason.Should().Be(5);
        modified.ParsedEpisode.Should().Be(10);
        original.ParsedSeason.Should().BeNull();
    }
}
