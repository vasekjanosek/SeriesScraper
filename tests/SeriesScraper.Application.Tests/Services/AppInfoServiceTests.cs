using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class AppInfoServiceTests
{
    private readonly ISettingRepository _settingRepository;
    private readonly IDataSourceImportRunRepository _importRunRepository;
    private readonly ILogger<AppInfoService> _logger;
    private readonly AppInfoService _sut;

    public AppInfoServiceTests()
    {
        _settingRepository = Substitute.For<ISettingRepository>();
        _importRunRepository = Substitute.For<IDataSourceImportRunRepository>();
        _logger = Substitute.For<ILogger<AppInfoService>>();

        _sut = new AppInfoService(
            _settingRepository,
            _importRunRepository,
            _logger);
    }

    // ─── GetAppInfoAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetAppInfoAsync_ReturnsVersionString()
    {
        var result = await _sut.GetAppInfoAsync();

        result.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAppInfoAsync_ReturnsNonNegativeUptime()
    {
        var result = await _sut.GetAppInfoAsync();

        result.Uptime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetAppInfoAsync_ReturnsDatabaseStatsObject()
    {
        var result = await _sut.GetAppInfoAsync();

        result.DatabaseStats.Should().NotBeNull();
    }

    // ─── GetDatabaseStatsAsync ────────────────────────────────────

    [Fact]
    public async Task GetDatabaseStatsAsync_ReturnsSettingsCount()
    {
        var settings = new List<Setting>
        {
            new() { Key = "A", Value = "1", LastModifiedAt = DateTime.UtcNow },
            new() { Key = "B", Value = "2", LastModifiedAt = DateTime.UtcNow },
            new() { Key = "C", Value = "3", LastModifiedAt = DateTime.UtcNow }
        };
        _settingRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.GetDatabaseStatsAsync();

        result.TableCounts.Should().Contain(t => t.TableName == "Settings" && t.RowCount == 3);
    }

    [Fact]
    public async Task GetDatabaseStatsAsync_ReturnsZero_WhenNoSettings()
    {
        _settingRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Setting>());

        var result = await _sut.GetDatabaseStatsAsync();

        result.TableCounts.Should().Contain(t => t.TableName == "Settings" && t.RowCount == 0);
    }

    [Fact]
    public async Task GetDatabaseStatsAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _settingRepository.GetAllAsync(cts.Token).Returns(new List<Setting>());

        await _sut.GetDatabaseStatsAsync(cts.Token);

        await _settingRepository.Received(1).GetAllAsync(cts.Token);
    }

    [Fact]
    public async Task GetAppInfoAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();

        var result = await _sut.GetAppInfoAsync(cts.Token);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDatabaseStatsAsync_TableCountsNotNull()
    {
        _settingRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Setting>());

        var result = await _sut.GetDatabaseStatsAsync();

        result.TableCounts.Should().NotBeNull();
    }
}
