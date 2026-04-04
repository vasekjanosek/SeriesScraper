using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Web.Pages;

namespace SeriesScraper.Web.Tests.Pages;

public class NotificationsTests : TestContext
{
    private readonly IWatchlistNotificationService _service;

    public NotificationsTests()
    {
        _service = Substitute.For<IWatchlistNotificationService>();
        Services.AddSingleton(_service);
    }

    // ─── Loading state ────────────────────────────────────────────

    [Fact]
    public async Task Notifications_EmptyList_ShowsNoNotificationsMessage()
    {
        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<WatchlistNotificationDto>());

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask); // wait for lifecycle

        cut.Markup.Should().Contain("No notifications yet");
    }

    // ─── Displays correct data ────────────────────────────────────

    [Fact]
    public async Task Notifications_WithItems_ShowsTitleLanguageQualityLinkAndTime()
    {
        var notifications = new List<WatchlistNotificationDto>
        {
            new()
            {
                WatchlistNotificationId = 1,
                WatchlistItemId = 10,
                WatchlistTitle = "Breaking Bad",
                LinkId = 100,
                LinkUrl = "https://example.com/download/bb.torrent",
                Language = "en",
                Quality = "1080p BluRay",
                CreatedAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
                IsRead = false
            }
        };

        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(notifications);

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.Markup.Should().Contain("Breaking Bad");
        cut.Markup.Should().Contain("en");
        cut.Markup.Should().Contain("1080p BluRay");
        cut.Markup.Should().Contain("https://example.com/download/bb.torrent");
        cut.Markup.Should().Contain("2026-04-01");
    }

    [Fact]
    public async Task Notifications_NullLanguageAndQuality_ShowsDash()
    {
        var notifications = new List<WatchlistNotificationDto>
        {
            new()
            {
                WatchlistNotificationId = 2,
                WatchlistItemId = 10,
                WatchlistTitle = "Test Show",
                LinkId = 101,
                LinkUrl = "https://example.com/file.zip",
                Language = null,
                Quality = null,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            }
        };

        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(notifications);

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        // null Language and Quality show as "—"
        cut.FindAll("td").Select(td => td.TextContent)
            .Count(t => t == "—").Should().BeGreaterThanOrEqualTo(2);
    }

    // ─── Mark read ────────────────────────────────────────────────

    [Fact]
    public async Task Notifications_MarkReadButton_CallsMarkAsReadAndReloads()
    {
        var notifications = new List<WatchlistNotificationDto>
        {
            new()
            {
                WatchlistNotificationId = 5,
                WatchlistItemId = 1,
                WatchlistTitle = "Show",
                LinkId = 1,
                LinkUrl = "https://example.com",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            }
        };

        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(notifications, new List<WatchlistNotificationDto>());

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        var markReadButton = cut.Find("button.btn-outline-primary");
        await cut.InvokeAsync(() => markReadButton.Click());

        await _service.Received(1).MarkAsReadAsync(5, Arg.Any<CancellationToken>());
        // After reload the list is empty, showing the empty message
        cut.Markup.Should().Contain("No notifications yet");
    }

    // ─── Mark all read ────────────────────────────────────────────

    [Fact]
    public async Task Notifications_MarkAllReadButton_CallsMarkAllAsReadAndReloads()
    {
        var notifications = new List<WatchlistNotificationDto>
        {
            new()
            {
                WatchlistNotificationId = 3,
                WatchlistItemId = 1,
                WatchlistTitle = "Show",
                LinkId = 1,
                LinkUrl = "https://example.com",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            }
        };

        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(notifications, new List<WatchlistNotificationDto>());

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        var markAllButton = cut.Find("button.btn-outline-secondary");
        await cut.InvokeAsync(() => markAllButton.Click());

        await _service.Received(1).MarkAllAsReadAsync(Arg.Any<CancellationToken>());
        cut.Markup.Should().Contain("No notifications yet");
    }

    [Fact]
    public async Task Notifications_AllRead_DoesNotShowMarkAllReadButton()
    {
        var notifications = new List<WatchlistNotificationDto>
        {
            new()
            {
                WatchlistNotificationId = 4,
                WatchlistItemId = 1,
                WatchlistTitle = "Show",
                LinkId = 1,
                LinkUrl = "https://example.com",
                CreatedAt = DateTime.UtcNow,
                IsRead = true
            }
        };

        _service.GetAllNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(notifications);

        var cut = RenderComponent<Notifications>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.FindAll("button.btn-outline-secondary").Should().BeEmpty();
    }
}
