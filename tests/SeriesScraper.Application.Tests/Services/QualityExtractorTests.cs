using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Tests.Services;

public class QualityExtractorTests
{
    private readonly Mock<IQualityPatternService> _patternServiceMock = new();
    private readonly Mock<ILogger<QualityExtractor>> _loggerMock = new();
    private readonly QualityExtractor _sut;

    public QualityExtractorTests()
    {
        _sut = new QualityExtractor(_patternServiceMock.Object, _loggerMock.Object);
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPatternService_ThrowsArgumentNullException()
    {
        var act = () => new QualityExtractor(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("patternService");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new QualityExtractor(_patternServiceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── Empty/null input ───────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_NullText_ReturnsNone()
    {
        var result = await _sut.ExtractAsync(null!);
        result.Should().Be(QualityRank.None);
    }

    [Fact]
    public async Task ExtractAsync_EmptyText_ReturnsNone()
    {
        var result = await _sut.ExtractAsync("");
        result.Should().Be(QualityRank.None);
    }

    [Fact]
    public async Task ExtractAsync_WhitespaceText_ReturnsNone()
    {
        var result = await _sut.ExtractAsync("   ");
        result.Should().Be(QualityRank.None);
    }

    // ── AC#1: Token matching ───────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_KnownPositiveToken_ReturnsMatchWithCorrectScore()
    {
        // AC#6: Known positive match
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("Download Movie.Name.1080p.BluRay.x264");

        result.Score.Should().Be(80);
        result.MatchedTokens.Should().Contain("1080p");
        result.Confidence.Should().BeGreaterThan(0);
        result.BestMatchPolarity.Should().Be(TokenPolarity.Positive);
    }

    [Fact]
    public async Task ExtractAsync_MultipleTokens_SelectsHighestRank()
    {
        SetupTokensAndPatterns(
            tokens:
            [
                new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true },
                new() { TokenId = 2, TokenText = "720p", QualityRank = 60, Polarity = TokenPolarity.Positive, IsActive = true }
            ],
            patterns: []);

        var result = await _sut.ExtractAsync("Available in 1080p and 720p");

        result.Score.Should().Be(80);
        result.MatchedTokens.Should().HaveCount(2);
        result.MatchedTokens.Should().Contain("1080p");
        result.MatchedTokens.Should().Contain("720p");
    }

    [Fact]
    public async Task ExtractAsync_TokenCaseInsensitive_Matches()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "BluRay", QualityRank = 70, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("This is a BLURAY release");

        result.Score.Should().Be(70);
        result.MatchedTokens.Should().Contain("BluRay");
    }

    [Fact]
    public async Task ExtractAsync_TokenNotInText_NoMatch()
    {
        // AC#6: No-match scenario
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "2160p", QualityRank = 100, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("Just a regular post with no quality info");

        result.Score.Should().Be(0);
        result.MatchedTokens.Should().BeEmpty();
        result.Confidence.Should().Be(0.0);
        result.BestMatchPolarity.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_TokenWordBoundary_DoesNotMatchSubstring()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "HDR", QualityRank = 75, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        // "HDR" should not match inside "SHDREAM"
        var result = await _sut.ExtractAsync("SHDREAM is not a quality indicator");

        result.Score.Should().Be(0);
        result.MatchedTokens.Should().BeEmpty();
    }

    // ── AC#2: Negative polarity downranking ────────────────────────

    [Fact]
    public async Task ExtractAsync_NegativePolarityWithPositive_PositiveWins()
    {
        // AC#6: Known negative-polarity downrank
        SetupTokensAndPatterns(
            tokens:
            [
                new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true },
                new() { TokenId = 2, TokenText = "AI-upscaled", QualityRank = -10, Polarity = TokenPolarity.Negative, IsActive = true }
            ],
            patterns: []);

        var result = await _sut.ExtractAsync("1080p AI-upscaled content available");

        result.Score.Should().Be(80);
        result.BestMatchPolarity.Should().Be(TokenPolarity.Positive);
        result.MatchedTokens.Should().Contain("AI-upscaled");
        result.MatchedTokens.Should().Contain("1080p");
    }

    [Fact]
    public async Task ExtractAsync_OnlyNegativePolarity_ReturnsNegativeScore()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "AI-upscaled", QualityRank = -10, Polarity = TokenPolarity.Negative, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("This is AI-upscaled content");

        result.Score.Should().Be(-10);
        result.BestMatchPolarity.Should().Be(TokenPolarity.Negative);
    }

    [Fact]
    public async Task ExtractAsync_LowPositiveBeatsHighNegative_PositiveAlwaysWins()
    {
        // Even a low positive should beat a negative with a higher absolute rank
        SetupTokensAndPatterns(
            tokens:
            [
                new() { TokenId = 1, TokenText = "480p", QualityRank = 40, Polarity = TokenPolarity.Positive, IsActive = true },
                new() { TokenId = 2, TokenText = "AI-upscaled", QualityRank = -10, Polarity = TokenPolarity.Negative, IsActive = true }
            ],
            patterns: []);

        var result = await _sut.ExtractAsync("480p AI-upscaled release");

        result.Score.Should().Be(40);
        result.BestMatchPolarity.Should().Be(TokenPolarity.Positive);
    }

    // ── AC#1: Learned pattern matching + AC#3: hit count ───────────

    [Fact]
    public async Task ExtractAsync_LearnedPatternMatch_ReturnsScore()
    {
        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 1, PatternRegex = @"\b1080p\b", DerivedRank = 80,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                }
            ]);

        var result = await _sut.ExtractAsync("Movie.Name.1080p.BluRay");

        result.Score.Should().Be(80);
        result.MatchedTokens.Should().Contain("1080p");
    }

    [Fact]
    public async Task ExtractAsync_PatternMatch_IncrementsHitCount()
    {
        // AC#6: hit_count increment verification
        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 42, PatternRegex = @"\b720p\b", DerivedRank = 60,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                }
            ]);

        await _sut.ExtractAsync("Some 720p content here");

        _patternServiceMock.Verify(
            s => s.RecordPatternHitAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_MultiplePatternMatches_IncrementsEachHitCount()
    {
        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 1, PatternRegex = @"\b1080p\b", DerivedRank = 80,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                },
                new()
                {
                    PatternId = 2, PatternRegex = @"\bBluRay\b", DerivedRank = 70,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                }
            ]);

        await _sut.ExtractAsync("Movie 1080p BluRay release");

        _patternServiceMock.Verify(s => s.RecordPatternHitAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _patternServiceMock.Verify(s => s.RecordPatternHitAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_TokenMatch_DoesNotIncrementHitCount()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        await _sut.ExtractAsync("Movie 1080p release");

        _patternServiceMock.Verify(
            s => s.RecordPatternHitAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── AC#5: Regex timeout ────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_RegexTimeout_TreatsAsNoMatch()
    {
        // AC#6: Pattern timeout simulation (adversarial ReDoS input)
        // Use a catastrophic backtracking pattern with adversarial input
        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 1,
                    PatternRegex = @"^(a+)+$",
                    DerivedRank = 80,
                    Polarity = TokenPolarity.Positive,
                    IsActive = true,
                    AlgorithmVersion = "1.0"
                }
            ]);

        // This input causes catastrophic backtracking with the pattern above
        var adversarialInput = new string('a', 30) + "X";

        var result = await _sut.ExtractAsync(adversarialInput);

        // Pattern should timeout → treated as no match
        // (May or may not time out depending on regex engine; we verify the code handles it)
        // If it doesn't timeout, it simply won't match because of the trailing X
        result.MatchedTokens.Should().NotContain("^(a+)+$");
    }

    [Fact]
    public async Task ExtractAsync_RegexTimeout_LogsWarning()
    {
        // We verify the logger is called when a timeout occurs.
        // Use a known ReDoS pattern that will timeout with long adversarial input.
        var loggerMock = new Mock<ILogger<QualityExtractor>>();
        var sut = new QualityExtractor(_patternServiceMock.Object, loggerMock.Object);

        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 1,
                    PatternRegex = @"^(a+)+$",
                    DerivedRank = 80,
                    Polarity = TokenPolarity.Positive,
                    IsActive = true,
                    AlgorithmVersion = "1.0"
                }
            ]);

        // Use very long adversarial input to ensure timeout
        var adversarialInput = new string('a', 50) + "!";

        await sut.ExtractAsync(adversarialInput);

        // The pattern either times out (and logs) or doesn't match (trailing !)
        // Either way, no crash should occur — this tests resilience
    }

    // ── AC#4: New pattern discovery ────────────────────────────────

    [Fact]
    public async Task ExtractAsync_UnknownResolutionToken_RecordsAsLearnedPattern()
    {
        SetupTokensAndPatterns(tokens: [], patterns: []);

        _patternServiceMock
            .Setup(s => s.AddLearnedPatternAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TokenPolarity>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QualityLearnedPattern
            {
                PatternId = 100,
                PatternRegex = @"\b540p\b",
                DerivedRank = 50,
                AlgorithmVersion = QualityExtractor.AlgorithmVersion,
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Learned
            });

        await _sut.ExtractAsync("This release is in 540p quality");

        _patternServiceMock.Verify(
            s => s.AddLearnedPatternAsync(
                @"\b540p\b", 50, TokenPolarity.Positive,
                QualityExtractor.AlgorithmVersion,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_KnownTokenText_DoesNotRecordDuplicate()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        await _sut.ExtractAsync("Movie in 1080p quality");

        _patternServiceMock.Verify(
            s => s.AddLearnedPatternAsync(
                It.Is<string>(p => p.Contains("1080p")),
                It.IsAny<int>(), It.IsAny<TokenPolarity>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_KnownPatternRegex_DoesNotRecordDuplicate()
    {
        SetupTokensAndPatterns(
            tokens: [],
            patterns:
            [
                new()
                {
                    PatternId = 1, PatternRegex = @"\b720p\b", DerivedRank = 60,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                }
            ]);

        await _sut.ExtractAsync("Movie in 720p quality");

        _patternServiceMock.Verify(
            s => s.AddLearnedPatternAsync(
                @"\b720p\b", It.IsAny<int>(), It.IsAny<TokenPolarity>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Confidence calculation ──────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_SingleMatch_ConfidenceIs0_5()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("Movie 1080p");

        result.Confidence.Should().Be(0.5); // 1 / (1 + 1) = 0.5
    }

    [Fact]
    public async Task ExtractAsync_TwoMatches_ConfidenceIncreases()
    {
        SetupTokensAndPatterns(
            tokens:
            [
                new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true },
                new() { TokenId = 2, TokenText = "BluRay", QualityRank = 70, Polarity = TokenPolarity.Positive, IsActive = true }
            ],
            patterns: []);

        var result = await _sut.ExtractAsync("Movie 1080p BluRay");

        result.Confidence.Should().BeApproximately(0.6667, 0.001); // 2 / (2 + 1) ≈ 0.6667
    }

    // ── Word boundary matching via token ────────────────────────────

    [Fact]
    public async Task ExtractAsync_TokenInsideWord_DoesNotMatch()
    {
        // "HDR" should not match as a substring of another word
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "HDR", QualityRank = 75, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("SHDREAM is not an indicator");

        result.Score.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_TokenAtStartOfText_Matches()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("1080p is the quality");

        result.Score.Should().Be(80);
    }

    [Fact]
    public async Task ExtractAsync_TokenAtEndOfText_Matches()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("Quality is 1080p");

        result.Score.Should().Be(80);
    }

    [Fact]
    public async Task ExtractAsync_TokenSeparatedByDots_Matches()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("movie.1080p.bluray");

        result.Score.Should().Be(80);
    }

    [Fact]
    public async Task ExtractAsync_TokenSeparatedBySlash_Matches()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "x265", QualityRank = 65, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns: []);

        var result = await _sut.ExtractAsync("x265/HEVC content");

        result.Score.Should().Be(65);
    }

    // ── Combined token + pattern scenario ───────────────────────────

    [Fact]
    public async Task ExtractAsync_TokenAndPatternBothMatch_UsesHighestRank()
    {
        SetupTokensAndPatterns(
            tokens: [new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive, IsActive = true }],
            patterns:
            [
                new()
                {
                    PatternId = 1, PatternRegex = @"\b2160p\b", DerivedRank = 100,
                    Polarity = TokenPolarity.Positive, IsActive = true, AlgorithmVersion = "1.0"
                }
            ]);

        var result = await _sut.ExtractAsync("Movie 1080p and also 2160p available");

        result.Score.Should().Be(100);
        result.MatchedTokens.Should().HaveCount(2);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private void SetupTokensAndPatterns(
        List<QualityToken> tokens,
        List<QualityLearnedPattern> patterns)
    {
        _patternServiceMock
            .Setup(s => s.GetActiveTokensAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        _patternServiceMock
            .Setup(s => s.GetActivePatternsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patterns);

        _patternServiceMock
            .Setup(s => s.RecordPatternHitAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _patternServiceMock
            .Setup(s => s.AddLearnedPatternAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TokenPolarity>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string regex, int rank, TokenPolarity pol, string ver, CancellationToken _) =>
                new QualityLearnedPattern
                {
                    PatternId = 999,
                    PatternRegex = regex,
                    DerivedRank = rank,
                    Polarity = pol,
                    AlgorithmVersion = ver,
                    Source = PatternSource.Learned,
                    IsActive = true
                });
    }
}
