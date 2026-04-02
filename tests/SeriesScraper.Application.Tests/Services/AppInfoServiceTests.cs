using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class AppInfoServiceTests
{
    private readonly IDatabaseStatsProvider _statsProvider;
    private readonly ILogger<AppInfoService> _logger;
    private readonly AppInfoService _sut;

    public AppInfoServiceTests()
    {
        _statsProvider = Substitute.For<IDatabaseStatsProvider>();
        _logger = Substitute.For<ILogger<AppInfoService>>();

        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TableRowCount>().AsReadOnly());
        _statsProvider.CheckConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        _sut = new AppInfoService(_statsProvider, _logger);
    }

    // ─── GetAppInfoAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetAppInfoAsync_ReturnsVersionString()
    {
        var result = await _sut.GetAppInfoAsync();

        result.Version.Should().NotBeNullOrEmpty();
        result.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public async Task GetAppInfoAsync_ReturnsNonNegativeUptime()
    {
        var result = await _sut.GetAppInfoAsync();

        result.Uptime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetAppInfoAsync_PopulatesDatabaseStats_NotEmpty()
    {
        var counts = new List<TableRowCount>
        {
            new() { TableName = "Forums", RowCount = 3 },
            new() { TableName = "MediaTitles", RowCount = 500 }
        };
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>())
            .Returns(counts.AsReadOnly());

        var result = await _sut.GetAppInfoAsync();

        result.DatabaseStats.Should().NotBeNull();
        result.DatabaseStats.TableCounts.Should().HaveCount(2);
        result.DatabaseStats.TableCounts.Should().Contain(t => t.TableName == "Forums" && t.RowCount == 3);
        result.DatabaseStats.TableCounts.Should().Contain(t => t.TableName == "MediaTitles" && t.RowCount == 500);
    }

    [Fact]
    public async Task GetAppInfoAsync_ReturnsDatabaseConnectedTrue_WhenConnectionSucceeds()
    {
        _statsProvider.CheckConnectionAsync(Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.GetAppInfoAsync();

        result.DatabaseConnected.Should().BeTrue();
    }

    [Fact]
    public async Task GetAppInfoAsync_ReturnsDatabaseConnectedFalse_WhenConnectionFails()
    {
        _statsProvider.CheckConnectionAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.GetAppInfoAsync();

        result.DatabaseConnected.Should().BeFalse();
    }

    [Fact]
    public async Task GetAppInfoAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();

        await _sut.GetAppInfoAsync(cts.Token);

        await _statsProvider.Received(1).CheckConnectionAsync(cts.Token);
        await _statsProvider.Received(1).GetTableRowCountsAsync(cts.Token);
    }

    // ─── GetDatabaseStatsAsync ────────────────────────────────────

    [Fact]
    public async Task GetDatabaseStatsAsync_ReturnsActualTableCounts()
    {
        var counts = new List<TableRowCount>
        {
            new() { TableName = "Forums", RowCount = 5 },
            new() { TableName = "MediaTitles", RowCount = 1200 },
            new() { TableName = "Links", RowCount = 300 },
            new() { TableName = "ScrapeRuns", RowCount = 10 },
            new() { TableName = "Settings", RowCount = 3 },
            new() { TableName = "LinkTypes", RowCount = 8 }
        };
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>())
            .Returns(counts.AsReadOnly());

        var result = await _sut.GetDatabaseStatsAsync();

        result.TableCounts.Should().HaveCount(6);
        result.TableCounts.Should().Contain(t => t.TableName == "Forums" && t.RowCount == 5);
        result.TableCounts.Should().Contain(t => t.TableName == "Links" && t.RowCount == 300);
    }

    [Fact]
    public async Task GetDatabaseStatsAsync_ReturnsEmptyList_WhenNoData()
    {
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TableRowCount>().AsReadOnly());

        var result = await _sut.GetDatabaseStatsAsync();

        result.TableCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDatabaseStatsAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _statsProvider.GetTableRowCountsAsync(cts.Token)
            .Returns(new List<TableRowCount>().AsReadOnly());

        await _sut.GetDatabaseStatsAsync(cts.Token);

        await _statsProvider.Received(1).GetTableRowCountsAsync(cts.Token);
    }
}
