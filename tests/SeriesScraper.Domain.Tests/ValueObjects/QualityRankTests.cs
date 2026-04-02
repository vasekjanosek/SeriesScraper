using FluentAssertions;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class QualityRankTests
{
    // ── None ────────────────────────────────────────────────────────

    [Fact]
    public void None_ReturnsZeroScore()
    {
        var rank = QualityRank.None;
        rank.Score.Should().Be(0);
    }

    [Fact]
    public void None_ReturnsEmptyMatchedTokens()
    {
        var rank = QualityRank.None;
        rank.MatchedTokens.Should().BeEmpty();
    }

    [Fact]
    public void None_ReturnsZeroConfidence()
    {
        var rank = QualityRank.None;
        rank.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void None_ReturnsNullBestMatchPolarity()
    {
        var rank = QualityRank.None;
        rank.BestMatchPolarity.Should().BeNull();
    }

    // ── Construction ────────────────────────────────────────────────

    [Fact]
    public void Construction_WithValues_SetsAllProperties()
    {
        var tokens = new List<string> { "1080p", "BluRay" };
        var rank = new QualityRank
        {
            Score = 80,
            MatchedTokens = tokens,
            Confidence = 0.6667,
            BestMatchPolarity = TokenPolarity.Positive
        };

        rank.Score.Should().Be(80);
        rank.MatchedTokens.Should().BeEquivalentTo(tokens);
        rank.Confidence.Should().Be(0.6667);
        rank.BestMatchPolarity.Should().Be(TokenPolarity.Positive);
    }

    [Fact]
    public void Construction_NegativePolarity_SetsCorrectly()
    {
        var rank = new QualityRank
        {
            Score = -10,
            MatchedTokens = new List<string> { "AI-upscaled" },
            Confidence = 0.5,
            BestMatchPolarity = TokenPolarity.Negative
        };

        rank.Score.Should().Be(-10);
        rank.BestMatchPolarity.Should().Be(TokenPolarity.Negative);
    }

    // ── Record equality ─────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var tokens = new List<string> { "1080p" };
        var a = new QualityRank { Score = 80, MatchedTokens = tokens, Confidence = 0.5, BestMatchPolarity = TokenPolarity.Positive };
        var b = new QualityRank { Score = 80, MatchedTokens = tokens, Confidence = 0.5, BestMatchPolarity = TokenPolarity.Positive };

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentScore_AreNotEqual()
    {
        var tokens = new List<string> { "1080p" };
        var a = new QualityRank { Score = 80, MatchedTokens = tokens, Confidence = 0.5 };
        var b = new QualityRank { Score = 60, MatchedTokens = tokens, Confidence = 0.5 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void DefaultMatchedTokens_IsEmptyList()
    {
        var rank = new QualityRank { Score = 0, Confidence = 0.0 };
        rank.MatchedTokens.Should().BeEmpty();
    }
}
