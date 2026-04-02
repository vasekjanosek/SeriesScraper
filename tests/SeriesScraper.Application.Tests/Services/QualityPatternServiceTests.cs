using FluentAssertions;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class QualityPatternServiceTests
{
    private readonly Mock<IQualityPatternRepository> _repoMock = new();
    private readonly Mock<ISettingRepository> _settingsMock = new();
    private readonly QualityPatternService _sut;

    public QualityPatternServiceTests()
    {
        _sut = new QualityPatternService(_repoMock.Object, _settingsMock.Object);
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        var act = () => new QualityPatternService(null!, _settingsMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_NullSettings_ThrowsArgumentNullException()
    {
        var act = () => new QualityPatternService(_repoMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    // ── GetActiveTokensAsync ───────────────────────────────────────

    [Fact]
    public async Task GetActiveTokensAsync_DelegatesToRepository()
    {
        var tokens = new List<QualityToken>
        {
            new() { TokenId = 1, TokenText = "1080p", QualityRank = 80, Polarity = TokenPolarity.Positive }
        };
        _repoMock.Setup(r => r.GetActiveTokensAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var result = await _sut.GetActiveTokensAsync();

        result.Should().BeEquivalentTo(tokens);
        _repoMock.Verify(r => r.GetActiveTokensAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetActivePatternsAsync ─────────────────────────────────────

    [Fact]
    public async Task GetActivePatternsAsync_DelegatesToRepository()
    {
        var patterns = new List<QualityLearnedPattern>
        {
            new() { PatternId = 1, PatternRegex = @"\b1080p\b", DerivedRank = 80, AlgorithmVersion = "1.0" }
        };
        _repoMock.Setup(r => r.GetActivePatternsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patterns);

        var result = await _sut.GetActivePatternsAsync();

        result.Should().BeEquivalentTo(patterns);
    }

    // ── AddLearnedPatternAsync ─────────────────────────────────────

    [Fact]
    public async Task AddLearnedPatternAsync_ValidInput_ReturnsPatternWithCorrectProperties()
    {
        _repoMock.Setup(r => r.AddPatternAsync(It.IsAny<QualityLearnedPattern>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.AddLearnedPatternAsync(
            @"\b1080p\b", 80, TokenPolarity.Positive, "1.0");

        result.PatternRegex.Should().Be(@"\b1080p\b");
        result.DerivedRank.Should().Be(80);
        result.Polarity.Should().Be(TokenPolarity.Positive);
        result.AlgorithmVersion.Should().Be("1.0");
        result.Source.Should().Be(PatternSource.Learned);
        result.HitCount.Should().Be(0);
        result.IsActive.Should().BeTrue();
        result.LastMatchedAt.Should().BeNull();
        _repoMock.Verify(r => r.AddPatternAsync(It.IsAny<QualityLearnedPattern>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLearnedPatternAsync_NullRegex_ThrowsArgumentException()
    {
        var act = () => _sut.AddLearnedPatternAsync(null!, 80, TokenPolarity.Positive, "1.0");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLearnedPatternAsync_EmptyRegex_ThrowsArgumentException()
    {
        var act = () => _sut.AddLearnedPatternAsync("", 80, TokenPolarity.Positive, "1.0");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLearnedPatternAsync_WhitespaceRegex_ThrowsArgumentException()
    {
        var act = () => _sut.AddLearnedPatternAsync("   ", 80, TokenPolarity.Positive, "1.0");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLearnedPatternAsync_NullAlgorithmVersion_ThrowsArgumentException()
    {
        var act = () => _sut.AddLearnedPatternAsync(@"\b1080p\b", 80, TokenPolarity.Positive, null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddLearnedPatternAsync_EmptyAlgorithmVersion_ThrowsArgumentException()
    {
        var act = () => _sut.AddLearnedPatternAsync(@"\b1080p\b", 80, TokenPolarity.Positive, "");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── RecordPatternHitAsync ──────────────────────────────────────

    [Fact]
    public async Task RecordPatternHitAsync_DelegatesToRepository()
    {
        _repoMock.Setup(r => r.IncrementHitCountAsync(5, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RecordPatternHitAsync(5);

        _repoMock.Verify(r => r.IncrementHitCountAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPruneCandidatesAsync ────────────────────────────────────

    [Fact]
    public async Task GetPruneCandidatesAsync_UsesThresholdFromSettings()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync("10");
        var candidates = new List<QualityLearnedPattern>();
        _repoMock.Setup(r => r.GetPruneCandidatesAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        var result = await _sut.GetPruneCandidatesAsync();

        result.Should().BeSameAs(candidates);
        _repoMock.Verify(r => r.GetPruneCandidatesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPruneCandidatesAsync_SettingMissing_UsesDefaultThreshold()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var candidates = new List<QualityLearnedPattern>();
        _repoMock.Setup(r => r.GetPruneCandidatesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        await _sut.GetPruneCandidatesAsync();

        _repoMock.Verify(r => r.GetPruneCandidatesAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPruneCandidatesAsync_SettingInvalid_UsesDefaultThreshold()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-a-number");
        var candidates = new List<QualityLearnedPattern>();
        _repoMock.Setup(r => r.GetPruneCandidatesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        await _sut.GetPruneCandidatesAsync();

        _repoMock.Verify(r => r.GetPruneCandidatesAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeactivatePatternsAsync ────────────────────────────────────

    [Fact]
    public async Task DeactivatePatternsAsync_DelegatesToRepository()
    {
        var ids = new[] { 1, 2, 3 };
        _repoMock.Setup(r => r.DeactivatePatternsAsync(ids, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeactivatePatternsAsync(ids);

        _repoMock.Verify(r => r.DeactivatePatternsAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPruningThresholdAsync ───────────────────────────────────

    [Fact]
    public async Task GetPruningThresholdAsync_ValidSetting_ReturnsParsedValue()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync("15");

        var result = await _sut.GetPruningThresholdAsync();

        result.Should().Be(15);
    }

    [Fact]
    public async Task GetPruningThresholdAsync_MissingSetting_ReturnsDefault()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetPruningThresholdAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetPruningThresholdAsync_InvalidSetting_ReturnsDefault()
    {
        _settingsMock.Setup(s => s.GetValueAsync("QualityPruningThreshold", It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc");

        var result = await _sut.GetPruningThresholdAsync();

        result.Should().Be(5);
    }
}
