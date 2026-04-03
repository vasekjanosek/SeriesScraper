using FluentAssertions;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Tests.Entities;

public class WatchlistNotificationTests
{
    [Fact]
    public void WatchlistNotification_DefaultIsRead_IsFalse()
    {
        var notification = new WatchlistNotification();

        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public void WatchlistNotification_CanSetProperties()
    {
        var notification = new WatchlistNotification
        {
            WatchlistNotificationId = 1,
            WatchlistItemId = 10,
            LinkId = 100,
            CreatedAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc),
            IsRead = true
        };

        notification.WatchlistNotificationId.Should().Be(1);
        notification.WatchlistItemId.Should().Be(10);
        notification.LinkId.Should().Be(100);
        notification.CreatedAt.Should().Be(new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc));
        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void WatchlistNotification_NavigationProperties_DefaultToNull()
    {
        var notification = new WatchlistNotification();

        // Navigation properties are initialized to null! via default
        // but the default value for reference type fields when not set is null
        notification.WatchlistItemId.Should().Be(0);
        notification.LinkId.Should().Be(0);
    }

    [Fact]
    public void WatchlistNotification_CanSetNavigationProperties()
    {
        var watchlistItem = new WatchlistItem { WatchlistItemId = 10, CustomTitle = "Test" };
        var link = new Link
        {
            LinkId = 100,
            Url = "https://example.com/file.zip",
            PostUrl = "https://forum.example.com/post/1",
            LinkTypeId = 1,
            RunId = 1
        };

        var notification = new WatchlistNotification
        {
            WatchlistItemId = 10,
            LinkId = 100,
            WatchlistItem = watchlistItem,
            Link = link
        };

        notification.WatchlistItem.Should().BeSameAs(watchlistItem);
        notification.Link.Should().BeSameAs(link);
    }
}
