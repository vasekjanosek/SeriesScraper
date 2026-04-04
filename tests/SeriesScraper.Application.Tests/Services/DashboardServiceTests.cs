using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Tests.Services;

public class DashboardServiceTests
{
    private readonly IForumRepository _forumRepository;
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly IRunProgressService _runProgressService;
    private readonly ISettingsService _settingsService;
    private readonly IWatchlistService _watchlistService;
    private readonly IDatabaseStatsProvider _statsProvider;
    private readonly IImdbImportTrigger _importTrigger;
    private readonly ILogger<DashboardService> _logger;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _forumRepository = Substitute.For<IForumRepository>();
        _scrapeRunRepository = Substitute.For<IScrapeRunRepository>();
        _runProgressService = Substitute.For<IRunProgressService>();
        _settingsService = Substitute.For<ISettingsService>();
        _watchlistService = Substitute.For<IWatchlistService>();
        _statsProvider = Substitute.For<IDatabaseStatsProvider>();
        _importTrigger = Substitute.For<IImdbImportTrigger>();
        _logger = Substitute.For<ILogger<DashboardService>>();
        _sut = new DashboardService(
            _forumRepository,
            _scrapeRunRepository,
            _runProgressService,
            _settingsService,
            _watchlistService,
            _statsProvider,
            _importTrigger,
            _logger);
    }

    // ── TriggerImportAsync ─────────────────────────────────────────────

    [Fact]
    public async Task TriggerImportAsync_CallsTriggerOnImportTrigger()
    {
        await _sut.TriggerImportAsync();

        _importTrigger.Received(1).TriggerImportNow();
    }

    // ── GetDashboardAsync — full aggregation ───────────────────────────

    [Fact]
    public async Task GetDashboardAsync_ReturnsAllSections()
    {
        SetupEmptyDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Should().NotBeNull();
        result.Forums.Should().NotBeNull();
        result.ImdbDataset.Should().NotBeNull();
        result.ActiveRuns.Should().NotBeNull();
        result.Watchlist.Should().NotBeNull();
    }

    // ── Forum Statuses ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_ReturnsEmptyForums_WhenNoneConfigured()
    {
        SetupEmptyDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Forums.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_MapsForumFields_Correctly()
    {
        var forums = new List<Forum>
        {
            CreateForum(1, "TestForum", "https://example.com", isActive: true),
            CreateForum(2, "InactiveForum", "https://other.com", isActive: false)
        };
        var lastCompleted = new Dictionary<int, DateTime>
        {
            { 1, new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc) }
        };

        _forumRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(forums);
        _scrapeRunRepository.GetLastCompletedTimePerForumAsync(Arg.Any<CancellationToken>()).Returns(lastCompleted);
        SetupOtherDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Forums.Should().HaveCount(2);

        result.Forums[0].ForumId.Should().Be(1);
        result.Forums[0].Name.Should().Be("TestForum");
        result.Forums[0].BaseUrl.Should().Be("https://example.com");
        result.Forums[0].IsActive.Should().BeTrue();
        result.Forums[0].ConnectivityStatus.Should().Be(ForumConnectivityStatus.Online);
        result.Forums[0].LastSuccessfulScrape.Should().Be(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));

        result.Forums[1].ForumId.Should().Be(2);
        result.Forums[1].IsActive.Should().BeFalse();
        result.Forums[1].ConnectivityStatus.Should().Be(ForumConnectivityStatus.Unknown);
        result.Forums[1].LastSuccessfulScrape.Should().BeNull();
    }

    [Fact]
    public async Task GetDashboardAsync_SetsLastScrapeToNull_WhenDefaultDateTime()
    {
        var forums = new List<Forum> { CreateForum(1, "F1", "https://f1.com", true) };
        var lastCompleted = new Dictionary<int, DateTime> { { 1, default } };

        _forumRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(forums);
        _scrapeRunRepository.GetLastCompletedTimePerForumAsync(Arg.Any<CancellationToken>()).Returns(lastCompleted);
        SetupOtherDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Forums[0].LastSuccessfulScrape.Should().BeNull();
    }

    // ── IMDB Dataset Status ────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_MapsImdbStatus_Correctly()
    {
        var imdbStatus = new ImdbImportStatusDto
        {
            LastImportDate = new DateTime(2026, 3, 30, 8, 0, 0, DateTimeKind.Utc),
            RowsImported = 100000,
            Status = "Complete",
            NextScheduledRun = new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc)
        };

        _settingsService.GetImdbImportStatusAsync(Arg.Any<CancellationToken>()).Returns(imdbStatus);
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>()).Returns(new List<TableRowCount>
        {
            new() { TableName = "media_titles", RowCount = 500000 }
        });
        SetupForumDefaults();
        SetupRunDefaults();
        SetupWatchlistDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ImdbDataset.LastImportDate.Should().Be(new DateTime(2026, 3, 30, 8, 0, 0, DateTimeKind.Utc));
        result.ImdbDataset.TitleCount.Should().Be(500000);
        result.ImdbDataset.NextScheduledRefresh.Should().Be(new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc));
        result.ImdbDataset.ImportStatus.Should().Be("Complete");
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsZeroTitleCount_WhenStatsProviderFails()
    {
        _settingsService.GetImdbImportStatusAsync(Arg.Any<CancellationToken>()).Returns(new ImdbImportStatusDto());
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("DB error"));
        SetupForumDefaults();
        SetupRunDefaults();
        SetupWatchlistDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ImdbDataset.TitleCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsZeroTitleCount_WhenMediaTitlesTableNotFound()
    {
        _settingsService.GetImdbImportStatusAsync(Arg.Any<CancellationToken>()).Returns(new ImdbImportStatusDto());
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>()).Returns(new List<TableRowCount>
        {
            new() { TableName = "other_table", RowCount = 100 }
        });
        SetupForumDefaults();
        SetupRunDefaults();
        SetupWatchlistDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ImdbDataset.TitleCount.Should().Be(0);
    }

    // ── Active Runs ────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_ReturnsEmptyActiveRuns_WhenNoRunning()
    {
        SetupEmptyDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ActiveRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_MapsActiveRuns_WithProgressPercent()
    {
        var runs = new List<RunProgressDto>
        {
            new()
            {
                RunId = 1,
                ForumName = "TestForum",
                Status = ScrapeRunStatus.Running,
                StartedAt = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
                TotalItems = 100,
                ProcessedItems = 75
            }
        };

        _runProgressService.GetActiveRunsAsync(Arg.Any<CancellationToken>()).Returns(runs);
        SetupForumDefaults();
        SetupImdbDefaults();
        SetupWatchlistDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ActiveRuns.Should().HaveCount(1);
        result.ActiveRuns[0].RunId.Should().Be(1);
        result.ActiveRuns[0].ForumName.Should().Be("TestForum");
        result.ActiveRuns[0].Status.Should().Be(ScrapeRunStatus.Running);
        result.ActiveRuns[0].ProgressPercent.Should().Be(75);
    }

    [Fact]
    public async Task GetDashboardAsync_SetsProgressToZero_WhenTotalItemsIsZero()
    {
        var runs = new List<RunProgressDto>
        {
            new()
            {
                RunId = 1,
                ForumName = "TestForum",
                Status = ScrapeRunStatus.Pending,
                StartedAt = DateTime.UtcNow,
                TotalItems = 0,
                ProcessedItems = 0
            }
        };

        _runProgressService.GetActiveRunsAsync(Arg.Any<CancellationToken>()).Returns(runs);
        SetupForumDefaults();
        SetupImdbDefaults();
        SetupWatchlistDefaults();

        var result = await _sut.GetDashboardAsync();

        result.ActiveRuns[0].ProgressPercent.Should().Be(0);
    }

    // ── Watchlist Summary ──────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_MapsWatchlistSummary_Correctly()
    {
        var items = new List<WatchlistItemDto>
        {
            new() { WatchlistItemId = 1, CustomTitle = "Breaking Bad", IsActive = true },
            new() { WatchlistItemId = 2, CustomTitle = "The Wire", IsActive = true },
            new() { WatchlistItemId = 3, CustomTitle = "Dexter", IsActive = false }
        };
        var matches = new List<WatchlistMatchDto>
        {
            new() { WatchlistItemId = 1, CustomTitle = "Breaking Bad", NewMatchCount = 3 },
            new() { WatchlistItemId = 2, CustomTitle = "The Wire", NewMatchCount = 1 }
        };

        _watchlistService.GetWatchlistAsync(Arg.Any<CancellationToken>()).Returns(items);
        _watchlistService.CheckNewMatchesAsync(Arg.Any<CancellationToken>()).Returns(matches);
        SetupForumDefaults();
        SetupImdbDefaults();
        SetupRunDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Watchlist.UnreadMatchCount.Should().Be(4);
        result.Watchlist.TotalWatchlistItems.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsZeroUnread_WhenNoMatches()
    {
        _watchlistService.GetWatchlistAsync(Arg.Any<CancellationToken>()).Returns(new List<WatchlistItemDto>());
        _watchlistService.CheckNewMatchesAsync(Arg.Any<CancellationToken>()).Returns(new List<WatchlistMatchDto>());
        SetupForumDefaults();
        SetupImdbDefaults();
        SetupRunDefaults();

        var result = await _sut.GetDashboardAsync();

        result.Watchlist.UnreadMatchCount.Should().Be(0);
        result.Watchlist.TotalWatchlistItems.Should().Be(0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void SetupEmptyDefaults()
    {
        SetupForumDefaults();
        SetupImdbDefaults();
        SetupRunDefaults();
        SetupWatchlistDefaults();
    }

    private void SetupForumDefaults()
    {
        _forumRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Forum>());
        _scrapeRunRepository.GetLastCompletedTimePerForumAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<int, DateTime>());
    }

    private void SetupImdbDefaults()
    {
        _settingsService.GetImdbImportStatusAsync(Arg.Any<CancellationToken>()).Returns(new ImdbImportStatusDto());
        _statsProvider.GetTableRowCountsAsync(Arg.Any<CancellationToken>()).Returns(new List<TableRowCount>());
    }

    private void SetupRunDefaults()
    {
        _runProgressService.GetActiveRunsAsync(Arg.Any<CancellationToken>()).Returns(new List<RunProgressDto>());
    }

    private void SetupWatchlistDefaults()
    {
        _watchlistService.GetWatchlistAsync(Arg.Any<CancellationToken>()).Returns(new List<WatchlistItemDto>());
        _watchlistService.CheckNewMatchesAsync(Arg.Any<CancellationToken>()).Returns(new List<WatchlistMatchDto>());
    }

    private void SetupOtherDefaults()
    {
        SetupImdbDefaults();
        SetupRunDefaults();
        SetupWatchlistDefaults();
    }

    private static Forum CreateForum(int id, string name, string baseUrl, bool isActive)
    {
        return new Forum
        {
            ForumId = id,
            Name = name,
            BaseUrl = baseUrl,
            Username = "user",
            CredentialKey = "FORUM_TEST_KEY",
            IsActive = isActive
        };
    }
}
