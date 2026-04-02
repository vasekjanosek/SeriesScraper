using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class EpisodeInfoTests
{
    [Fact]
    public void EpisodeInfo_StoresSeasonAndEpisode()
    {
        var ep = new EpisodeInfo
        {
            Season = 3,
            EpisodeNumber = 7
        };

        ep.Season.Should().Be(3);
        ep.EpisodeNumber.Should().Be(7);
        ep.Title.Should().BeNull();
    }

    [Fact]
    public void EpisodeInfo_WithTitle_StoresTitle()
    {
        var ep = new EpisodeInfo
        {
            Season = 1,
            EpisodeNumber = 1,
            Title = "Pilot"
        };

        ep.Title.Should().Be("Pilot");
    }

    [Fact]
    public void EpisodeInfo_WithSameValues_AreEqual()
    {
        var a = new EpisodeInfo { Season = 1, EpisodeNumber = 2, Title = "Ep" };
        var b = new EpisodeInfo { Season = 1, EpisodeNumber = 2, Title = "Ep" };

        a.Should().Be(b);
    }
}
