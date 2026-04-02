using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class WatchlistServiceTests
{
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly IMediaTitleRepository _mediaTitleRepository;
    private readonly IMediaRatingRepository _mediaRatingRepository;
    private readonly ILogger<WatchlistService> _logger;
    private readonly WatchlistService _sut;

    public WatchlistServiceTests()
    {
        _watchlistRepository = Substitute.For<IWatchlistRepository>();
        _mediaTitleRepository = Substitute.For<IMediaTitleRepository>();
        _mediaRatingRepository = Substitute.For<IMediaRatingRepository>();
        _logger = Substitute.For<ILogger<WatchlistService>>();

        _sut = new WatchlistService(
            _watchlistRepository,
            _mediaTitleRepository,
            _mediaRatingRepository,
            _logger);
    }

    // ─── AddToWatchlistByMediaTitleAsync ───────────────────────────

    [Fact]
    public async Task AddToWatchlistByMediaTitleAsync_AddsItem_WhenMediaTitleExists()
    {
        var mediaTitle = new MediaTitle
        {
            MediaId = 1,
            CanonicalTitle = "Breaking Bad",
            Type = MediaType.Series,
            Year = 2008,
            SourceId = 1
        };

        _watchlistRepository.ExistsByMediaTitleIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(false);
        _mediaTitleRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(mediaTitle);
        _watchlistRepository.AddAsync(Arg.Any<WatchlistItem>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<WatchlistItem>();
                item.WatchlistItemId = 42;
                return item;
            });

        var result = await _sut.AddToWatchlistByMediaTitleAsync(1);

        result.WatchlistItemId.Should().Be(42);
        result.MediaTitleId.Should().Be(1);
        result.CustomTitle.Should().Be("Breaking Bad");
        result.IsActive.Should().BeTrue();
        result.NotificationPreference.Should().Be(NotificationPreference.None);
    }

    [Fact]
    public async Task AddToWatchlistByMediaTitleAsync_Throws_WhenAlreadyOnWatchlist()
    {
        _watchlistRepository.ExistsByMediaTitleIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _sut.AddToWatchlistByMediaTitleAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already on the watchlist*");
    }

    [Fact]
    public async Task AddToWatchlistByMediaTitleAsync_Throws_WhenMediaTitleNotFound()
    {
        _watchlistRepository.ExistsByMediaTitleIdAsync(99, Arg.Any<CancellationToken>())
            .Returns(false);
        _mediaTitleRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((MediaTitle?)null);

        var act = () => _sut.AddToWatchlistByMediaTitleAsync(99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task AddToWatchlistByMediaTitleAsync_SetsAddedAtToUtcNow()
    {
        var before = DateTime.UtcNow;
        _watchlistRepository.ExistsByMediaTitleIdAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _mediaTitleRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new MediaTitle { MediaId = 1, CanonicalTitle = "Test", SourceId = 1 });
        _watchlistRepository.AddAsync(Arg.Any<WatchlistItem>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<WatchlistItem>());

        var result = await _sut.AddToWatchlistByMediaTitleAsync(1);

        result.AddedAt.Should().BeOnOrAfter(before);
        result.AddedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    // ─── AddToWatchlistByCustomTitleAsync ──────────────────────────

    [Fact]
    public async Task AddToWatchlistByCustomTitleAsync_AddsItem()
    {
        _watchlistRepository.AddAsync(Arg.Any<WatchlistItem>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<WatchlistItem>();
                item.WatchlistItemId = 10;
                return item;
            });

        var result = await _sut.AddToWatchlistByCustomTitleAsync("My Custom Show");

        result.WatchlistItemId.Should().Be(10);
        result.CustomTitle.Should().Be("My Custom Show");
        result.MediaTitleId.Should().BeNull();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddToWatchlistByCustomTitleAsync_TrimsTitle()
    {
        _watchlistRepository.AddAsync(Arg.Any<WatchlistItem>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<WatchlistItem>());

        var result = await _sut.AddToWatchlistByCustomTitleAsync("  My Show  ");

        result.CustomTitle.Should().Be("My Show");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddToWatchlistByCustomTitleAsync_Throws_WhenTitleEmpty(string? title)
    {
        var act = () => _sut.AddToWatchlistByCustomTitleAsync(title!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    // ─── RemoveFromWatchlistAsync ──────────────────────────────────

    [Fact]
    public async Task RemoveFromWatchlistAsync_CallsRepository()
    {
        await _sut.RemoveFromWatchlistAsync(5);

        await _watchlistRepository.Received(1).RemoveAsync(5, Arg.Any<CancellationToken>());
    }

    // ─── GetWatchlistAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetWatchlistAsync_ReturnsAllItems()
    {
        var items = new List<WatchlistItem>
        {
            new()
            {
                WatchlistItemId = 1,
                CustomTitle = "Show A",
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                NotificationPreference = NotificationPreference.OnNewLinks
            },
            new()
            {
                WatchlistItemId = 2,
                CustomTitle = "Show B",
                MediaTitleId = 5,
                AddedAt = DateTime.UtcNow,
                IsActive = false,
                MediaTitle = new MediaTitle
                {
                    MediaId = 5,
                    CanonicalTitle = "Show B",
                    Type = MediaType.Movie,
                    Year = 2020,
                    SourceId = 1
                }
            }
        };

        _watchlistRepository.GetAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        _mediaRatingRepository.GetByMediaIdAndSourceAsync(5, 1, Arg.Any<CancellationToken>())
            .Returns(new MediaRating { MediaId = 5, SourceId = 1, Rating = 8.5m, VoteCount = 1000 });

        var result = await _sut.GetWatchlistAsync();

        result.Should().HaveCount(2);
        result[0].CustomTitle.Should().Be("Show A");
        result[0].MediaType.Should().BeNull();
        result[1].CustomTitle.Should().Be("Show B");
        result[1].MediaType.Should().Be("Movie");
        result[1].ImdbRating.Should().Be(8.5m);
        result[1].ImdbVoteCount.Should().Be(1000);
    }

    [Fact]
    public async Task GetWatchlistAsync_ReturnsEmpty_WhenNoItems()
    {
        _watchlistRepository.GetAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WatchlistItem>().AsReadOnly() as IReadOnlyList<WatchlistItem>);

        var result = await _sut.GetWatchlistAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWatchlistAsync_HandlesNullRating()
    {
        var items = new List<WatchlistItem>
        {
            new()
            {
                WatchlistItemId = 1,
                CustomTitle = "No Rating Show",
                MediaTitleId = 3,
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                MediaTitle = new MediaTitle
                {
                    MediaId = 3,
                    CanonicalTitle = "No Rating Show",
                    Type = MediaType.Series,
                    Year = 2023,
                    SourceId = 1
                }
            }
        };

        _watchlistRepository.GetAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        _mediaRatingRepository.GetByMediaIdAndSourceAsync(3, 1, Arg.Any<CancellationToken>())
            .Returns((MediaRating?)null);

        var result = await _sut.GetWatchlistAsync();

        result[0].ImdbRating.Should().BeNull();
        result[0].ImdbVoteCount.Should().BeNull();
        result[0].MediaType.Should().Be("Series");
    }

    // ─── IsOnWatchlistAsync ────────────────────────────────────────

    [Fact]
    public async Task IsOnWatchlistAsync_ReturnsTrue_WhenExists()
    {
        _watchlistRepository.ExistsByMediaTitleIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.IsOnWatchlistAsync(1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOnWatchlistAsync_ReturnsFalse_WhenNotExists()
    {
        _watchlistRepository.ExistsByMediaTitleIdAsync(99, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.IsOnWatchlistAsync(99);

        result.Should().BeFalse();
    }

    // ─── ToggleActiveAsync ─────────────────────────────────────────

    [Fact]
    public async Task ToggleActiveAsync_TogglesFromActiveToInactive()
    {
        var item = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Test",
            IsActive = true
        };

        _watchlistRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(item);

        await _sut.ToggleActiveAsync(1);

        item.IsActive.Should().BeFalse();
        await _watchlistRepository.Received(1).UpdateAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleActiveAsync_TogglesFromInactiveToActive()
    {
        var item = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Test",
            IsActive = false
        };

        _watchlistRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(item);

        await _sut.ToggleActiveAsync(1);

        item.IsActive.Should().BeTrue();
        await _watchlistRepository.Received(1).UpdateAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleActiveAsync_Throws_WhenNotFound()
    {
        _watchlistRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((WatchlistItem?)null);

        var act = () => _sut.ToggleActiveAsync(99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── UpdateNotificationPreferenceAsync ─────────────────────────

    [Fact]
    public async Task UpdateNotificationPreferenceAsync_UpdatesPreference()
    {
        var item = new WatchlistItem
        {
            WatchlistItemId = 1,
            CustomTitle = "Test",
            NotificationPreference = NotificationPreference.None
        };

        _watchlistRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(item);

        await _sut.UpdateNotificationPreferenceAsync(1, NotificationPreference.OnNewLinks);

        item.NotificationPreference.Should().Be(NotificationPreference.OnNewLinks);
        await _watchlistRepository.Received(1).UpdateAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateNotificationPreferenceAsync_Throws_WhenNotFound()
    {
        _watchlistRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((WatchlistItem?)null);

        var act = () => _sut.UpdateNotificationPreferenceAsync(99, NotificationPreference.OnNewEpisodes);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── CheckNewMatchesAsync ──────────────────────────────────────

    [Fact]
    public async Task CheckNewMatchesAsync_ReturnsMappedMatches()
    {
        var item1 = new WatchlistItem { WatchlistItemId = 1, CustomTitle = "Show A" };
        var item2 = new WatchlistItem { WatchlistItemId = 2, CustomTitle = "Show B" };

        _watchlistRepository.GetItemsWithNewMatchesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<(WatchlistItem, int)>
            {
                (item1, 3),
                (item2, 1)
            }.AsReadOnly());

        var result = await _sut.CheckNewMatchesAsync();

        result.Should().HaveCount(2);
        result[0].WatchlistItemId.Should().Be(1);
        result[0].CustomTitle.Should().Be("Show A");
        result[0].NewMatchCount.Should().Be(3);
        result[1].WatchlistItemId.Should().Be(2);
        result[1].NewMatchCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckNewMatchesAsync_ReturnsEmpty_WhenNoMatches()
    {
        _watchlistRepository.GetItemsWithNewMatchesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(WatchlistItem, int)>().AsReadOnly()
                as IReadOnlyList<(WatchlistItem Item, int NewMatchCount)>);

        var result = await _sut.CheckNewMatchesAsync();

        result.Should().BeEmpty();
    }

    // ─── MapToDto enrichment ───────────────────────────────────────

    [Fact]
    public async Task GetWatchlistAsync_FetchesMediaTitle_WhenNavigationPropertyNull()
    {
        var items = new List<WatchlistItem>
        {
            new()
            {
                WatchlistItemId = 1,
                CustomTitle = "Fetched Title",
                MediaTitleId = 7,
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                MediaTitle = null // navigation property not loaded
            }
        };

        var mediaTitle = new MediaTitle
        {
            MediaId = 7,
            CanonicalTitle = "Fetched Title",
            Type = MediaType.Movie,
            Year = 2019,
            SourceId = 1
        };

        _watchlistRepository.GetAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        _mediaTitleRepository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(mediaTitle);
        _mediaRatingRepository.GetByMediaIdAndSourceAsync(7, 1, Arg.Any<CancellationToken>())
            .Returns(new MediaRating { MediaId = 7, SourceId = 1, Rating = 7.2m, VoteCount = 500 });

        var result = await _sut.GetWatchlistAsync();

        result[0].MediaType.Should().Be("Movie");
        result[0].Year.Should().Be(2019);
        result[0].ImdbRating.Should().Be(7.2m);
    }

    [Fact]
    public async Task GetWatchlistAsync_HandlesNullMediaTitle_WhenGetByIdReturnsNull()
    {
        var items = new List<WatchlistItem>
        {
            new()
            {
                WatchlistItemId = 1,
                CustomTitle = "Orphaned",
                MediaTitleId = 99,
                AddedAt = DateTime.UtcNow,
                IsActive = true,
                MediaTitle = null
            }
        };

        _watchlistRepository.GetAllAsync(false, Arg.Any<CancellationToken>())
            .Returns(items.AsReadOnly());
        _mediaTitleRepository.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((MediaTitle?)null);

        var result = await _sut.GetWatchlistAsync();

        result[0].MediaType.Should().BeNull();
        result[0].Year.Should().BeNull();
        result[0].ImdbRating.Should().BeNull();
    }

    // ─── DTO record tests ──────────────────────────────────────────

    [Fact]
    public void WatchlistItemDto_RecordEquality()
    {
        var dto1 = new WatchlistItemDto
        {
            WatchlistItemId = 1,
            CustomTitle = "Test",
            IsActive = true,
            NotificationPreference = NotificationPreference.None,
            AddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var dto2 = new WatchlistItemDto
        {
            WatchlistItemId = 1,
            CustomTitle = "Test",
            IsActive = true,
            NotificationPreference = NotificationPreference.None,
            AddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void WatchlistMatchDto_RecordEquality()
    {
        var dto1 = new WatchlistMatchDto { WatchlistItemId = 1, CustomTitle = "X", NewMatchCount = 3 };
        var dto2 = new WatchlistMatchDto { WatchlistItemId = 1, CustomTitle = "X", NewMatchCount = 3 };

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void WatchlistItemDto_DefaultValues()
    {
        var dto = new WatchlistItemDto
        {
            WatchlistItemId = 0,
            CustomTitle = "Default",
            IsActive = false,
            NotificationPreference = NotificationPreference.None,
            AddedAt = default
        };

        dto.MediaTitleId.Should().BeNull();
        dto.MediaType.Should().BeNull();
        dto.Year.Should().BeNull();
        dto.ImdbRating.Should().BeNull();
        dto.ImdbVoteCount.Should().BeNull();
        dto.LastMatchedAt.Should().BeNull();
    }
}
