using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class WatchlistNotificationServiceTests
{
    private readonly IWatchlistNotificationRepository _notificationRepository;
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly ILinkRepository _linkRepository;
    private readonly ILogger<WatchlistNotificationService> _logger;
    private readonly WatchlistNotificationService _sut;

    public WatchlistNotificationServiceTests()
    {
        _notificationRepository = Substitute.For<IWatchlistNotificationRepository>();
        _watchlistRepository = Substitute.For<IWatchlistRepository>();
        _linkRepository = Substitute.For<ILinkRepository>();
        _logger = Substitute.For<ILogger<WatchlistNotificationService>>();

        _sut = new WatchlistNotificationService(
            _notificationRepository,
            _watchlistRepository,
            _linkRepository,
            _logger);
    }

    // ─── Constructor Validation ───────────────────────────────────

    [Fact]
    public void Constructor_NullNotificationRepository_Throws()
    {
        var act = () => new WatchlistNotificationService(null!, _watchlistRepository, _linkRepository, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("notificationRepository");
    }

    [Fact]
    public void Constructor_NullWatchlistRepository_Throws()
    {
        var act = () => new WatchlistNotificationService(_notificationRepository, null!, _linkRepository, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("watchlistRepository");
    }

    [Fact]
    public void Constructor_NullLinkRepository_Throws()
    {
        var act = () => new WatchlistNotificationService(_notificationRepository, _watchlistRepository, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("linkRepository");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new WatchlistNotificationService(_notificationRepository, _watchlistRepository, _linkRepository, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ─── CreateNotificationsForRunAsync ───────────────────────────

    [Fact]
    public async Task CreateNotificationsForRunAsync_NoActiveWatchlistItems_SkipsProcessing()
    {
        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem>());

        await _sut.CreateNotificationsForRunAsync(1);

        await _linkRepository.DidNotReceive().GetCurrentByRunIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _notificationRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<WatchlistNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_NoLinks_SkipsProcessing()
    {
        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem>
            {
                new() { WatchlistItemId = 1, CustomTitle = "Test", MediaTitleId = 10, NotificationPreference = NotificationPreference.OnNewLinks, IsActive = true }
            });
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<Link>());

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<WatchlistNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_MatchingTitle_CreatesNotification()
    {
        var watchlistItem = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Breaking Bad",
            MediaTitleId = 10,
            NotificationPreference = NotificationPreference.OnNewLinks,
            IsActive = true
        };

        var link = new Link
        {
            LinkId = 100,
            Url = "https://download.example.com/file.zip",
            PostUrl = "https://forum.example.com/posts/breaking-bad-s01",
            RunId = 1,
            LinkTypeId = 1
        };

        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem> { watchlistItem });
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<Link> { link });

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<WatchlistNotification>>(n => n.Count() == 1 &&
                n.First().WatchlistItemId == 1 &&
                n.First().LinkId == 100 &&
                !n.First().IsRead),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_NonMatchingTitle_DoesNotCreateNotification()
    {
        var watchlistItem = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Breaking Bad",
            MediaTitleId = 10,
            NotificationPreference = NotificationPreference.OnNewLinks,
            IsActive = true
        };

        var link = new Link
        {
            LinkId = 100,
            Url = "https://download.example.com/file.zip",
            PostUrl = "https://forum.example.com/posts/game-of-thrones-s01",
            RunId = 1,
            LinkTypeId = 1
        };

        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem> { watchlistItem });
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<Link> { link });

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.DidNotReceive().AddRangeAsync(
            Arg.Any<IEnumerable<WatchlistNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_NonePreference_SkipsItem()
    {
        var watchlistItem = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Breaking Bad",
            MediaTitleId = 10,
            NotificationPreference = NotificationPreference.None,
            IsActive = true
        };

        var link = new Link
        {
            LinkId = 100,
            Url = "https://download.example.com/file.zip",
            PostUrl = "https://forum.example.com/posts/breaking-bad-s01",
            RunId = 1,
            LinkTypeId = 1
        };

        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem> { watchlistItem });
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<Link> { link });

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.DidNotReceive().AddRangeAsync(
            Arg.Any<IEnumerable<WatchlistNotification>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_CustomTitleMatch_CreatesNotification()
    {
        var watchlistItem = new WatchlistItem
        {
            WatchlistItemId = 2,
            CustomTitle = "Stranger Things",
            MediaTitleId = null,
            NotificationPreference = NotificationPreference.OnNewLinks,
            IsActive = true
        };

        var link = new Link
        {
            LinkId = 200,
            Url = "https://download.example.com/file2.zip",
            PostUrl = "https://forum.example.com/posts/stranger-things-s04",
            RunId = 1,
            LinkTypeId = 1
        };

        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistItem> { watchlistItem });
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new List<Link> { link });

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<WatchlistNotification>>(n => n.Count() == 1 &&
                n.First().WatchlistItemId == 2 &&
                n.First().LinkId == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotificationsForRunAsync_DeduplicatesByWatchlistItemAndLink()
    {
        // Two watchlist items with same title but different IDs; both match same link
        var items = new List<WatchlistItem>
        {
            new() { WatchlistItemId = 1, CustomTitle = "Breaking Bad", MediaTitleId = 10, NotificationPreference = NotificationPreference.OnNewLinks, IsActive = true },
        };

        // Two links with same PostUrl (both match)
        var links = new List<Link>
        {
            new() { LinkId = 100, Url = "https://dl.example.com/a.zip", PostUrl = "https://forum.example.com/posts/breaking-bad-s01", RunId = 1, LinkTypeId = 1 },
            new() { LinkId = 101, Url = "https://dl.example.com/b.zip", PostUrl = "https://forum.example.com/posts/breaking-bad-s02", RunId = 1, LinkTypeId = 1 },
        };

        _watchlistRepository.GetAllAsync(activeOnly: true, Arg.Any<CancellationToken>())
            .Returns(items);
        _linkRepository.GetCurrentByRunIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(links);

        await _sut.CreateNotificationsForRunAsync(1);

        await _notificationRepository.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<WatchlistNotification>>(n => n.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    // ─── GetAllNotificationsAsync ─────────────────────────────────

    [Fact]
    public async Task GetAllNotificationsAsync_ReturnsAllMappedDtos()
    {
        var notifications = new List<WatchlistNotification>
        {
            new()
            {
                WatchlistNotificationId = 1,
                WatchlistItemId = 10,
                LinkId = 100,
                CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                IsRead = false,
                WatchlistItem = new WatchlistItem { WatchlistItemId = 10, CustomTitle = "Breaking Bad" },
                Link = new Link { LinkId = 100, Url = "https://dl.example.com/bb.zip", PostUrl = "https://forum.example.com/bb", LinkTypeId = 1, RunId = 1 }
            },
            new()
            {
                WatchlistNotificationId = 2,
                WatchlistItemId = 20,
                LinkId = 200,
                CreatedAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                IsRead = true,
                WatchlistItem = new WatchlistItem { WatchlistItemId = 20, CustomTitle = "GoT" },
                Link = new Link { LinkId = 200, Url = "https://dl.example.com/got.zip", PostUrl = "https://forum.example.com/got", LinkTypeId = 1, RunId = 1 }
            }
        };

        _notificationRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(notifications);

        var result = await _sut.GetAllNotificationsAsync();

        result.Should().HaveCount(2);
        result[0].WatchlistNotificationId.Should().Be(1);
        result[0].WatchlistTitle.Should().Be("Breaking Bad");
        result[0].LinkUrl.Should().Be("https://dl.example.com/bb.zip");
        result[0].IsRead.Should().BeFalse();
        result[1].IsRead.Should().BeTrue();
    }

    // ─── GetUnreadNotificationsAsync ──────────────────────────────

    [Fact]
    public async Task GetUnreadNotificationsAsync_ReturnsOnlyUnread()
    {
        var notifications = new List<WatchlistNotification>
        {
            new()
            {
                WatchlistNotificationId = 1,
                WatchlistItemId = 10,
                LinkId = 100,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                WatchlistItem = new WatchlistItem { WatchlistItemId = 10, CustomTitle = "Test" },
                Link = new Link { LinkId = 100, Url = "https://dl.example.com/a.zip", PostUrl = "https://forum.example.com/a", LinkTypeId = 1, RunId = 1 }
            }
        };

        _notificationRepository.GetUnreadAsync(Arg.Any<CancellationToken>())
            .Returns(notifications);

        var result = await _sut.GetUnreadNotificationsAsync();

        result.Should().HaveCount(1);
        result[0].IsRead.Should().BeFalse();
    }

    // ─── GetUnreadCountAsync ──────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        _notificationRepository.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns(5);

        var result = await _sut.GetUnreadCountAsync();

        result.Should().Be(5);
    }

    // ─── MarkAsReadAsync ──────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_DelegatesToRepository()
    {
        await _sut.MarkAsReadAsync(42);

        await _notificationRepository.Received(1).MarkAsReadAsync(42, Arg.Any<CancellationToken>());
    }

    // ─── MarkAllAsReadAsync ───────────────────────────────────────

    [Fact]
    public async Task MarkAllAsReadAsync_DelegatesToRepository()
    {
        await _sut.MarkAllAsReadAsync();

        await _notificationRepository.Received(1).MarkAllAsReadAsync(Arg.Any<CancellationToken>());
    }
}
