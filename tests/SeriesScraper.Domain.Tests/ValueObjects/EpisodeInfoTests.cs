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
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void EpisodeInfo_WithDifferentSeason_AreNotEqual()
    {
        var a = new EpisodeInfo { Season = 1, EpisodeNumber = 1 };
        var b = new EpisodeInfo { Season = 2, EpisodeNumber = 1 };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void EpisodeInfo_WithDifferentEpisodeNumber_AreNotEqual()
    {
        var a = new EpisodeInfo { Season = 1, EpisodeNumber = 1 };
        var b = new EpisodeInfo { Season = 1, EpisodeNumber = 2 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void EpisodeInfo_WithDifferentTitle_AreNotEqual()
    {
        var a = new EpisodeInfo { Season = 1, EpisodeNumber = 1, Title = "A" };
        var b = new EpisodeInfo { Season = 1, EpisodeNumber = 1, Title = "B" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void EpisodeInfo_NullTitleVsSetTitle_AreNotEqual()
    {
        var a = new EpisodeInfo { Season = 1, EpisodeNumber = 1 };
        var b = new EpisodeInfo { Season = 1, EpisodeNumber = 1, Title = "Pilot" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void EpisodeInfo_GetHashCode_SameForEqualInstances()
    {
        var a = new EpisodeInfo { Season = 2, EpisodeNumber = 5, Title = "Test" };
        var b = new EpisodeInfo { Season = 2, EpisodeNumber = 5, Title = "Test" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void EpisodeInfo_ToString_ContainsTypeName()
    {
        var ep = new EpisodeInfo { Season = 1, EpisodeNumber = 1, Title = "Pilot" };

        ep.ToString().Should().Contain("EpisodeInfo");
    }

    [Fact]
    public void EpisodeInfo_WithExpression_CreatesModifiedCopy()
    {
        var original = new EpisodeInfo { Season = 1, EpisodeNumber = 1, Title = "Pilot" };
        var modified = original with { Title = "Changed" };

        modified.Title.Should().Be("Changed");
        original.Title.Should().Be("Pilot");
    }
}
