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
    }
}
