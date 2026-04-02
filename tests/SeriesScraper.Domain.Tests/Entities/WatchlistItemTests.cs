using FluentAssertions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Tests.Entities;

public class WatchlistItemTests
{
    [Fact]
    public void WatchlistItem_DefaultIsActive_IsTrue()
    {
        var item = new WatchlistItem { CustomTitle = "Test" };

        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public void WatchlistItem_DefaultNotificationPreference_IsNone()
    {
        var item = new WatchlistItem { CustomTitle = "Test" };

        item.NotificationPreference.Should().Be(NotificationPreference.None);
    }

    [Fact]
    public void WatchlistItem_MediaTitleId_DefaultsToNull()
    {
        var item = new WatchlistItem { CustomTitle = "Test" };

        item.MediaTitleId.Should().BeNull();
    }

    [Fact]
    public void WatchlistItem_LastMatchedAt_DefaultsToNull()
    {
        var item = new WatchlistItem { CustomTitle = "Test" };

        item.LastMatchedAt.Should().BeNull();
    }

    [Fact]
    public void WatchlistItem_CanSetMediaTitle()
    {
        var mediaTitle = new MediaTitle
        {
            MediaId = 1,
            CanonicalTitle = "Breaking Bad",
            Type = MediaType.Series
        };

        var item = new WatchlistItem
        {
            CustomTitle = "Breaking Bad",
            MediaTitleId = 1,
            MediaTitle = mediaTitle
        };

        item.MediaTitle.Should().BeSameAs(mediaTitle);
        item.MediaTitleId.Should().Be(1);
    }

    [Fact]
    public void WatchlistItem_RequiresCustomTitle()
    {
        // Required property enforced by compiler — test that it can be set
        var item = new WatchlistItem { CustomTitle = "My Title" };

        item.CustomTitle.Should().Be("My Title");
    }

    [Theory]
    [InlineData(NotificationPreference.None)]
    [InlineData(NotificationPreference.OnNewLinks)]
    [InlineData(NotificationPreference.OnNewEpisodes)]
    public void WatchlistItem_CanSetAllNotificationPreferences(NotificationPreference pref)
    {
        var item = new WatchlistItem
        {
            CustomTitle = "Test",
            NotificationPreference = pref
        };

        item.NotificationPreference.Should().Be(pref);
    }

    [Fact]
    public void NotificationPreference_HasExpectedValues()
    {
        Enum.GetValues<NotificationPreference>().Should().HaveCount(3);
        Enum.IsDefined(NotificationPreference.None).Should().BeTrue();
        Enum.IsDefined(NotificationPreference.OnNewLinks).Should().BeTrue();
        Enum.IsDefined(NotificationPreference.OnNewEpisodes).Should().BeTrue();
    }

    [Fact]
    public void WatchlistItem_CanToggleIsActive()
    {
        var item = new WatchlistItem { CustomTitle = "Test", IsActive = true };

        item.IsActive = false;
        item.IsActive.Should().BeFalse();

        item.IsActive = true;
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public void WatchlistItem_CanSetAddedAt()
    {
        var now = DateTime.UtcNow;
        var item = new WatchlistItem { CustomTitle = "Test", AddedAt = now };

        item.AddedAt.Should().Be(now);
    }

    [Fact]
    public void WatchlistItem_CanSetLastMatchedAt()
    {
        var now = DateTime.UtcNow;
        var item = new WatchlistItem { CustomTitle = "Test", LastMatchedAt = now };

        item.LastMatchedAt.Should().Be(now);
    }
}
