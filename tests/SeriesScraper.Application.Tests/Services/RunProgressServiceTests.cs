using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class RunProgressServiceTests
{
    private readonly IScrapeRunRepository _runRepository;
    private readonly IScrapeRunItemRepository _itemRepository;
    private readonly ILogger<RunProgressService> _logger;
    private readonly RunProgressService _sut;

    public RunProgressServiceTests()
    {
        _runRepository = Substitute.For<IScrapeRunRepository>();
        _itemRepository = Substitute.For<IScrapeRunItemRepository>();
        _logger = Substitute.For<ILogger<RunProgressService>>();
        _sut = new RunProgressService(_runRepository, _itemRepository, _logger);
    }

    // ── GetActiveRunsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetActiveRunsAsync_ReturnsEmptyList_WhenNoActiveRuns()
    {
        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ScrapeRun>());

        var result = await _sut.GetActiveRunsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveRunsAsync_ReturnsMappedDtos_ForActiveRuns()
    {
        var forum = new Forum { ForumId = 1, Name = "TestForum", BaseUrl = "https://example.com", Username = "user", CredentialKey = "ENV_KEY" };
        var runs = new List<ScrapeRun>
        {
            CreateRun(1, 1, ScrapeRunStatus.Running, forum, totalItems: 10, processedItems: 5,
                items: new List<ScrapeRunItem>
                {
                    CreateItem(1, 1, ScrapeRunItemStatus.Done, "https://example.com/1"),
                    CreateItem(2, 1, ScrapeRunItemStatus.Done, "https://example.com/2"),
                    CreateItem(3, 1, ScrapeRunItemStatus.Processing, "https://example.com/3"),
                    CreateItem(4, 1, ScrapeRunItemStatus.Pending, "https://example.com/4"),
                    CreateItem(5, 1, ScrapeRunItemStatus.Failed, "https://example.com/5"),
                }),
            CreateRun(2, 1, ScrapeRunStatus.Pending, forum, totalItems: 3, processedItems: 0,
                items: new List<ScrapeRunItem>())
        };

        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(runs);

        var result = await _sut.GetActiveRunsAsync();

        result.Should().HaveCount(2);

        // First run
        result[0].RunId.Should().Be(1);
        result[0].ForumName.Should().Be("TestForum");
        result[0].Status.Should().Be(ScrapeRunStatus.Running);
        result[0].TotalItems.Should().Be(10);
        result[0].ProcessedItems.Should().Be(5);
        result[0].CompletedItems.Should().Be(2);
        result[0].FailedItems.Should().Be(1);
        result[0].PendingItems.Should().Be(1);
        result[0].CurrentItem.Should().Be("https://example.com/3");
        result[0].Items.Should().HaveCount(5);

        // Second run (no items yet)
        result[1].RunId.Should().Be(2);
        result[1].Status.Should().Be(ScrapeRunStatus.Pending);
        result[1].CompletedItems.Should().Be(0);
        result[1].CurrentItem.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveRunsAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _runRepository.GetActiveRunsAsync(cts.Token)
            .Returns(Array.Empty<ScrapeRun>());

        await _sut.GetActiveRunsAsync(cts.Token);

        await _runRepository.Received(1).GetActiveRunsAsync(cts.Token);
    }

    [Fact]
    public async Task GetActiveRunsAsync_ForumNameFallback_WhenForumIsNull()
    {
        var run = CreateRun(1, 42, ScrapeRunStatus.Running, null!, totalItems: 0, processedItems: 0,
            items: new List<ScrapeRunItem>());
        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { run });

        var result = await _sut.GetActiveRunsAsync();

        result[0].ForumName.Should().Be("Forum #42");
    }

    [Fact]
    public async Task GetActiveRunsAsync_MapsItemStatuses_Correctly()
    {
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(1, 1, ScrapeRunStatus.Running, forum, totalItems: 3, processedItems: 1,
            items: new List<ScrapeRunItem>
            {
                CreateItem(10, 1, ScrapeRunItemStatus.Done, "https://x.com/a", DateTime.UtcNow),
                CreateItem(11, 1, ScrapeRunItemStatus.Skipped, "https://x.com/b"),
                CreateItem(12, 1, ScrapeRunItemStatus.Pending, "https://x.com/c"),
            });

        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { run });

        var result = await _sut.GetActiveRunsAsync();

        result[0].Items[0].Status.Should().Be(ScrapeRunItemStatus.Done);
        result[0].Items[0].RunItemId.Should().Be(10);
        result[0].Items[1].Status.Should().Be(ScrapeRunItemStatus.Skipped);
        result[0].Items[2].Status.Should().Be(ScrapeRunItemStatus.Pending);
    }

    // ── GetRunProgressAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetRunProgressAsync_ReturnsNull_WhenRunNotFound()
    {
        _runRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((ScrapeRun?)null);

        var result = await _sut.GetRunProgressAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRunProgressAsync_ReturnsMappedDto_WhenRunExists()
    {
        var forum = new Forum { ForumId = 2, Name = "Forum2", BaseUrl = "https://f2.com", Username = "u2", CredentialKey = "K2" };
        var run = CreateRun(5, 2, ScrapeRunStatus.Running, forum, totalItems: 4, processedItems: 2,
            items: new List<ScrapeRunItem>());

        var items = new List<ScrapeRunItem>
        {
            CreateItem(20, 5, ScrapeRunItemStatus.Done, "https://f2.com/1"),
            CreateItem(21, 5, ScrapeRunItemStatus.Done, "https://f2.com/2"),
            CreateItem(22, 5, ScrapeRunItemStatus.Processing, "https://f2.com/3"),
            CreateItem(23, 5, ScrapeRunItemStatus.Pending, "https://f2.com/4"),
        };

        _runRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(5, Arg.Any<CancellationToken>()).Returns(items);

        var result = await _sut.GetRunProgressAsync(5);

        result.Should().NotBeNull();
        result!.RunId.Should().Be(5);
        result.ForumId.Should().Be(2);
        result.ForumName.Should().Be("Forum2");
        result.Status.Should().Be(ScrapeRunStatus.Running);
        result.TotalItems.Should().Be(4);
        result.ProcessedItems.Should().Be(2);
        result.CompletedItems.Should().Be(2);
        result.FailedItems.Should().Be(0);
        result.PendingItems.Should().Be(1);
        result.CurrentItem.Should().Be("https://f2.com/3");
        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetRunProgressAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _runRepository.GetByIdAsync(1, cts.Token).Returns((ScrapeRun?)null);

        await _sut.GetRunProgressAsync(1, cts.Token);

        await _runRepository.Received(1).GetByIdAsync(1, cts.Token);
    }

    [Fact]
    public async Task GetRunProgressAsync_UsesItemRepository_NotRunItems()
    {
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(3, 1, ScrapeRunStatus.Running, forum, totalItems: 1, processedItems: 0,
            items: new List<ScrapeRunItem>
            {
                // Items on the run entity (should be ignored)
                CreateItem(100, 3, ScrapeRunItemStatus.Done, "https://x.com/old")
            });

        // Items from the repository (should be used)
        var repoItems = new List<ScrapeRunItem>
        {
            CreateItem(200, 3, ScrapeRunItemStatus.Failed, "https://x.com/new")
        };

        _runRepository.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(3, Arg.Any<CancellationToken>()).Returns(repoItems);

        var result = await _sut.GetRunProgressAsync(3);

        result!.Items.Should().HaveCount(1);
        result.Items[0].RunItemId.Should().Be(200);
        result.Items[0].Status.Should().Be(ScrapeRunItemStatus.Failed);
        result.FailedItems.Should().Be(1);
        result.CompletedItems.Should().Be(0);
    }

    [Fact]
    public async Task GetRunProgressAsync_NoCurrentItem_WhenNoneProcessing()
    {
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(7, 1, ScrapeRunStatus.Running, forum, totalItems: 2, processedItems: 2,
            items: new List<ScrapeRunItem>());

        var items = new List<ScrapeRunItem>
        {
            CreateItem(30, 7, ScrapeRunItemStatus.Done, "https://x.com/1"),
            CreateItem(31, 7, ScrapeRunItemStatus.Done, "https://x.com/2"),
        };

        _runRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(7, Arg.Any<CancellationToken>()).Returns(items);

        var result = await _sut.GetRunProgressAsync(7);

        result!.CurrentItem.Should().BeNull();
    }

    [Fact]
    public async Task GetRunProgressAsync_CompletedRun_MapsCorrectly()
    {
        var completedAt = DateTime.UtcNow;
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(8, 1, ScrapeRunStatus.Complete, forum, totalItems: 1, processedItems: 1,
            items: new List<ScrapeRunItem>());
        run.CompletedAt = completedAt;

        var items = new List<ScrapeRunItem>
        {
            CreateItem(40, 8, ScrapeRunItemStatus.Done, "https://x.com/1", completedAt),
        };

        _runRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(8, Arg.Any<CancellationToken>()).Returns(items);

        var result = await _sut.GetRunProgressAsync(8);

        result!.Status.Should().Be(ScrapeRunStatus.Complete);
        result.CompletedAt.Should().Be(completedAt);
        result.CompletedItems.Should().Be(1);
    }

    [Fact]
    public async Task GetRunProgressAsync_MapsStartedAt()
    {
        var startedAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(9, 1, ScrapeRunStatus.Pending, forum, totalItems: 0, processedItems: 0,
            items: new List<ScrapeRunItem>());
        run.StartedAt = startedAt;

        _runRepository.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(9, Arg.Any<CancellationToken>()).Returns(Array.Empty<ScrapeRunItem>());

        var result = await _sut.GetRunProgressAsync(9);

        result!.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public async Task GetRunProgressAsync_EmptyItems_ReturnsZeroCounts()
    {
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(10, 1, ScrapeRunStatus.Pending, forum, totalItems: 0, processedItems: 0,
            items: new List<ScrapeRunItem>());

        _runRepository.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(run);
        _itemRepository.GetByRunIdAsync(10, Arg.Any<CancellationToken>()).Returns(Array.Empty<ScrapeRunItem>());

        var result = await _sut.GetRunProgressAsync(10);

        result!.CompletedItems.Should().Be(0);
        result.FailedItems.Should().Be(0);
        result.PendingItems.Should().Be(0);
        result.CurrentItem.Should().BeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveRunsAsync_MapsItemProcessedAt()
    {
        var processedAt = new DateTime(2026, 4, 1, 14, 30, 0, DateTimeKind.Utc);
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(11, 1, ScrapeRunStatus.Running, forum, totalItems: 1, processedItems: 1,
            items: new List<ScrapeRunItem>
            {
                CreateItem(50, 11, ScrapeRunItemStatus.Done, "https://x.com/1", processedAt)
            });

        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { run });

        var result = await _sut.GetActiveRunsAsync();

        result[0].Items[0].ProcessedAt.Should().Be(processedAt);
        result[0].Items[0].PostUrl.Should().Be("https://x.com/1");
    }

    [Fact]
    public async Task GetActiveRunsAsync_MapsCompletedAt()
    {
        var forum = new Forum { ForumId = 1, Name = "F", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(12, 1, ScrapeRunStatus.Running, forum, totalItems: 0, processedItems: 0,
            items: new List<ScrapeRunItem>());
        run.CompletedAt = null;

        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { run });

        var result = await _sut.GetActiveRunsAsync();

        result[0].CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveRunsAsync_MapsForumId()
    {
        var forum = new Forum { ForumId = 7, Name = "F7", BaseUrl = "https://x.com", Username = "u", CredentialKey = "K" };
        var run = CreateRun(13, 7, ScrapeRunStatus.Pending, forum, totalItems: 0, processedItems: 0,
            items: new List<ScrapeRunItem>());

        _runRepository.GetActiveRunsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { run });

        var result = await _sut.GetActiveRunsAsync();

        result[0].ForumId.Should().Be(7);
    }

    // ── Helpers ─────────────────────────────────────────────

    private static ScrapeRun CreateRun(int runId, int forumId, ScrapeRunStatus status, Forum forum,
        int totalItems, int processedItems, ICollection<ScrapeRunItem> items)
    {
        return new ScrapeRun
        {
            RunId = runId,
            ForumId = forumId,
            Status = status,
            StartedAt = DateTime.UtcNow,
            TotalItems = totalItems,
            ProcessedItems = processedItems,
            Forum = forum,
            Items = items
        };
    }

    private static ScrapeRunItem CreateItem(int itemId, int runId, ScrapeRunItemStatus status,
        string postUrl, DateTime? processedAt = null)
    {
        return new ScrapeRunItem
        {
            RunItemId = itemId,
            RunId = runId,
            Status = status,
            PostUrl = postUrl,
            ProcessedAt = processedAt
        };
    }
}
