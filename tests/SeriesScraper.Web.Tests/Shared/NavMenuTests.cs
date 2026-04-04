using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Web.Shared;

namespace SeriesScraper.Web.Tests.Shared;

public class NavMenuTests : TestContext
{
    private readonly IWatchlistNotificationService _service;

    public NavMenuTests()
    {
        _service = Substitute.For<IWatchlistNotificationService>();
        Services.AddSingleton(_service);
    }

    // ─── Badge visibility ─────────────────────────────────────────

    [Fact]
    public async Task NavMenu_UnreadCountGreaterThanZero_ShowsBadgeWithCount()
    {
        _service.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns(3);

        var cut = RenderComponent<NavMenu>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        var badge = cut.Find("span.badge.bg-danger");
        badge.Should().NotBeNull();
        badge.TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public async Task NavMenu_UnreadCountZero_HidesBadge()
    {
        _service.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var cut = RenderComponent<NavMenu>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.FindAll("span.badge.bg-danger").Should().BeEmpty();
    }

    [Fact]
    public async Task NavMenu_ServiceThrows_DefaultsToZeroAndHidesBadge()
    {
        _service.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("DB unavailable")));

        var cut = RenderComponent<NavMenu>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.FindAll("span.badge.bg-danger").Should().BeEmpty();
    }

    // ─── Navigation links ─────────────────────────────────────────

    [Fact]
    public async Task NavMenu_Always_ContainsNotificationsLink()
    {
        _service.GetUnreadCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var cut = RenderComponent<NavMenu>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.Markup.Should().Contain("href=\"notifications\"");
    }
}
