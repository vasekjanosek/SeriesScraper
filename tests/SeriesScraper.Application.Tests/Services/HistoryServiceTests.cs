using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class HistoryServiceTests
{
    private readonly IScrapeRunRepository _scrapeRunRepository;
    private readonly ILogger<HistoryService> _logger;
    private readonly HistoryService _sut;

    public HistoryServiceTests()
    {
        _scrapeRunRepository = Substitute.For<IScrapeRunRepository>();
        _logger = Substitute.For<ILogger<HistoryService>>();
        _sut = new HistoryService(_scrapeRunRepository, _logger);
    }

    // ─── GetRunHistoryAsync ────────────────────────────────────────

    [Fact]
    public async Task GetRunHistoryAsync_ReturnsPagedResults()
    {
        var items = new List<RunHistorySummaryDto>
        {
            CreateDto(1, "Forum A", "Complete", 10, 10, 25, 5),
            CreateDto(2, "Forum B", "Failed", 5, 3, 8, 1)
        };

        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((items.AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 2));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), 1, 20);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetRunHistoryAsync_ClampsPageToMinimum1()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), page: -5, pageSize: 20);

        result.Page.Should().Be(1);
        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunHistoryAsync_ClampsPageSizeToDefault20_WhenZero()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), page: 1, pageSize: 0);

        result.PageSize.Should().Be(20);
        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 20, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunHistoryAsync_ClampsPageSizeToDefault20_WhenNegative()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), page: 1, pageSize: -10);

        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetRunHistoryAsync_ClampsPageSizeToMaximum100()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 100, Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), page: 1, pageSize: 500);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetRunHistoryAsync_PassesFilterAndSortToRepository()
    {
        var filter = new RunHistoryFilterDto
        {
            ForumId = 3,
            StatusFilter = "Complete",
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 3, 31)
        };

        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        await _sut.GetRunHistoryAsync(filter, 2, 10, "forum", true);

        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Is<RunHistoryFilterDto>(f =>
                f.ForumId == 3 &&
                f.StatusFilter == "Complete" &&
                f.DateFrom == new DateTime(2026, 1, 1) &&
                f.DateTo == new DateTime(2026, 3, 31)),
            2, 10, "forum", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunHistoryAsync_DefaultSortAndDirection()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), 1, 20);

        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), 1, 20, null, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunHistoryAsync_EmptyResults_ReturnsEmptyPage()
    {
        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), 1, 20);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetRunHistoryAsync_CalculatesTotalPagesCorrectly()
    {
        var items = new List<RunHistorySummaryDto>
        {
            CreateDto(1, "Forum A", "Complete", 10, 10, 20, 5)
        };

        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((items.AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 45));

        var result = await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), 1, 20);

        result.TotalCount.Should().Be(45);
        result.TotalPages.Should().Be(3); // ceil(45/20) = 3
    }

    [Fact]
    public async Task GetRunHistoryAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        await _sut.GetRunHistoryAsync(new RunHistoryFilterDto(), 1, 20, ct: token);

        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), token);
    }

    [Fact]
    public async Task GetRunHistoryAsync_WithNullFilter_UsesEmptyFilter()
    {
        var emptyFilter = new RunHistoryFilterDto();

        _scrapeRunRepository.GetRunHistoryPagedAsync(
            Arg.Any<RunHistoryFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<RunHistorySummaryDto>().AsReadOnly() as IReadOnlyList<RunHistorySummaryDto>, 0));

        await _sut.GetRunHistoryAsync(emptyFilter, 1, 20);

        await _scrapeRunRepository.Received(1).GetRunHistoryPagedAsync(
            Arg.Is<RunHistoryFilterDto>(f =>
                f.ForumId == null &&
                f.StatusFilter == null &&
                f.DateFrom == null &&
                f.DateTo == null),
            1, 20, null, false, Arg.Any<CancellationToken>());
    }

    // ─── GetRunSummaryAsync ────────────────────────────────────────

    [Fact]
    public async Task GetRunSummaryAsync_ReturnsRunSummary()
    {
        var expected = CreateDto(42, "TestForum", "Complete", 100, 95, 200, 50);

        _scrapeRunRepository.GetRunSummaryByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.GetRunSummaryAsync(42);

        result.Should().NotBeNull();
        result!.RunId.Should().Be(42);
        result.ForumName.Should().Be("TestForum");
        result.Status.Should().Be("Complete");
        result.TotalItems.Should().Be(100);
        result.ProcessedItems.Should().Be(95);
        result.LinkCount.Should().Be(200);
        result.MatchCount.Should().Be(50);
    }

    [Fact]
    public async Task GetRunSummaryAsync_NotFound_ReturnsNull()
    {
        _scrapeRunRepository.GetRunSummaryByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((RunHistorySummaryDto?)null);

        var result = await _sut.GetRunSummaryAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRunSummaryAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _scrapeRunRepository.GetRunSummaryByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((RunHistorySummaryDto?)null);

        await _sut.GetRunSummaryAsync(1, token);

        await _scrapeRunRepository.Received(1).GetRunSummaryByIdAsync(1, token);
    }

    [Fact]
    public async Task GetRunSummaryAsync_CompletedRun_HasDuration()
    {
        var start = new DateTime(2026, 4, 1, 10, 0, 0);
        var end = new DateTime(2026, 4, 1, 10, 15, 30);

        var dto = new RunHistorySummaryDto
        {
            RunId = 1,
            ForumName = "TestForum",
            StartedAt = start,
            CompletedAt = end,
            Status = "Complete",
            TotalItems = 10,
            ProcessedItems = 10,
            LinkCount = 5,
            MatchCount = 3
        };

        _scrapeRunRepository.GetRunSummaryByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(dto);

        var result = await _sut.GetRunSummaryAsync(1);

        result!.Duration.Should().NotBeNull();
        result.Duration!.Value.TotalMinutes.Should().BeApproximately(15.5, 0.01);
    }

    [Fact]
    public async Task GetRunSummaryAsync_InProgressRun_NullDuration()
    {
        var dto = new RunHistorySummaryDto
        {
            RunId = 1,
            ForumName = "TestForum",
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = "Running",
            TotalItems = 10,
            ProcessedItems = 5,
            LinkCount = 2,
            MatchCount = 1
        };

        _scrapeRunRepository.GetRunSummaryByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(dto);

        var result = await _sut.GetRunSummaryAsync(1);

        result!.Duration.Should().BeNull();
    }

    // ─── DTO record tests ──────────────────────────────────────────

    [Fact]
    public void RunHistorySummaryDto_Duration_CalculatedFromStartAndEnd()
    {
        var dto = new RunHistorySummaryDto
        {
            RunId = 1,
            ForumName = "Test",
            StartedAt = new DateTime(2026, 1, 1, 12, 0, 0),
            CompletedAt = new DateTime(2026, 1, 1, 13, 30, 45),
            Status = "Complete",
            TotalItems = 0,
            ProcessedItems = 0,
            LinkCount = 0,
            MatchCount = 0
        };

        dto.Duration.Should().Be(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void RunHistorySummaryDto_Duration_NullWhenNoCompletedAt()
    {
        var dto = new RunHistorySummaryDto
        {
            RunId = 1,
            ForumName = "Test",
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = "Running",
            TotalItems = 0,
            ProcessedItems = 0,
            LinkCount = 0,
            MatchCount = 0
        };

        dto.Duration.Should().BeNull();
    }

    [Fact]
    public void RunHistoryFilterDto_DefaultValues_AllNull()
    {
        var filter = new RunHistoryFilterDto();

        filter.DateFrom.Should().BeNull();
        filter.DateTo.Should().BeNull();
        filter.ForumId.Should().BeNull();
        filter.StatusFilter.Should().BeNull();
    }

    [Fact]
    public void RunHistoryFilterDto_WithValues()
    {
        var filter = new RunHistoryFilterDto
        {
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 12, 31),
            ForumId = 5,
            StatusFilter = "Complete"
        };

        filter.DateFrom.Should().Be(new DateTime(2026, 1, 1));
        filter.DateTo.Should().Be(new DateTime(2026, 12, 31));
        filter.ForumId.Should().Be(5);
        filter.StatusFilter.Should().Be("Complete");
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private static RunHistorySummaryDto CreateDto(
        int runId, string forumName, string status,
        int totalItems, int processedItems, int linkCount, int matchCount)
    {
        return new RunHistorySummaryDto
        {
            RunId = runId,
            ForumName = forumName,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            Status = status,
            TotalItems = totalItems,
            ProcessedItems = processedItems,
            LinkCount = linkCount,
            MatchCount = matchCount
        };
    }
}
