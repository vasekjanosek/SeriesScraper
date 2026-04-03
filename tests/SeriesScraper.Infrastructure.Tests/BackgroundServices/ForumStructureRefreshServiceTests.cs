using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.BackgroundServices;

namespace SeriesScraper.Infrastructure.Tests.BackgroundServices;

public class ForumStructureRefreshServiceTests
{
    private readonly IForumRepository _forumRepository = Substitute.For<IForumRepository>();
    private readonly IForumSectionDiscoveryService _discoveryService = Substitute.For<IForumSectionDiscoveryService>();
    private readonly IForumSectionRepository _sectionRepository = Substitute.For<IForumSectionRepository>();
    private readonly ISettingRepository _settingRepository = Substitute.For<ISettingRepository>();

    private Forum CreateForum(int id = 1, string name = "TestForum") => new()
    {
        ForumId = id,
        Name = name,
        BaseUrl = $"https://forum{id}.example.com",
        Username = "user",
        CredentialKey = "FORUM_TEST_PASS",
        IsActive = true,
        CrawlDepth = 1
    };

    private ForumSection CreateSection(int sectionId, int forumId, string url, string name, bool isActive = true) => new()
    {
        SectionId = sectionId,
        ForumId = forumId,
        Url = url,
        Name = name,
        IsActive = isActive,
        LastCrawledAt = DateTime.UtcNow
    };

    // --- ExecuteAsync / startup tests ---

    [Fact]
    public async Task ExecuteAsync_RunsInitialRefreshOnStart()
    {
        var refreshCalled = new TaskCompletionSource<bool>();
        var forum = CreateForum();
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                refreshCalled.TrySetResult(true);
                return new List<Forum> { forum }.AsReadOnly();
            });
        _sectionRepository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());
        _discoveryService.DiscoverSectionsAsync(forum, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        var wasCalled = await refreshCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        wasCalled.Should().BeTrue();
        await _forumRepository.Received().GetActiveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefullyOnCancellation()
    {
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum>().AsReadOnly());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        var act = async () => await service.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // --- RefreshAllForumsAsync tests ---

    [Fact]
    public async Task RefreshAllForumsAsync_CallsDiscoveryForEachActiveForum()
    {
        var forum1 = CreateForum(1, "Forum1");
        var forum2 = CreateForum(2, "Forum2");
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum1, forum2 }.AsReadOnly());
        _sectionRepository.GetByForumIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());
        _discoveryService.DiscoverSectionsAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        var service = CreateService();
        await service.RefreshAllForumsAsync(CancellationToken.None);

        await _discoveryService.Received(1).DiscoverSectionsAsync(forum1, Arg.Any<CancellationToken>());
        await _discoveryService.Received(1).DiscoverSectionsAsync(forum2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAllForumsAsync_ContinuesWhenOneForumFails()
    {
        var forum1 = CreateForum(1, "FailForum");
        var forum2 = CreateForum(2, "SuccessForum");
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum1, forum2 }.AsReadOnly());

        _sectionRepository.GetByForumIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        // First forum throws, second succeeds
        _discoveryService.DiscoverSectionsAsync(forum1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Network error"));
        _discoveryService.DiscoverSectionsAsync(forum2, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Second forum should still have been processed
        await _discoveryService.Received(1).DiscoverSectionsAsync(forum2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAllForumsAsync_NoForums_CompletesWithoutError()
    {
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum>().AsReadOnly());

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        await _discoveryService.DidNotReceive()
            .DiscoverSectionsAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAllForumsAsync_LogsNewSections()
    {
        var forum = CreateForum();
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum }.AsReadOnly());

        // No existing sections
        _sectionRepository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        // Discovery finds new sections
        var newSection = CreateSection(1, forum.ForumId, "https://forum1.example.com/section1", "New Section");
        _discoveryService.DiscoverSectionsAsync(forum, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { newSection }.AsReadOnly());

        var service = CreateService();

        // Should complete without error — logging is internal
        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAllForumsAsync_LogsRemovedSections()
    {
        var forum = CreateForum();
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum }.AsReadOnly());

        // Existing active section
        var existingSection = CreateSection(1, forum.ForumId, "https://forum1.example.com/old", "Old Section");
        _sectionRepository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existingSection }.AsReadOnly());

        // Discovery finds nothing (old section was removed)
        _discoveryService.DiscoverSectionsAsync(forum, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAllForumsAsync_LogsUpdatedSections()
    {
        var forum = CreateForum();
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum }.AsReadOnly());

        var existingSection = CreateSection(1, forum.ForumId, "https://forum1.example.com/s1", "Section");
        _sectionRepository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { existingSection }.AsReadOnly());

        // Same URL returned = updated (not new or removed)
        var updatedSection = CreateSection(1, forum.ForumId, "https://forum1.example.com/s1", "Section Updated");
        _discoveryService.DiscoverSectionsAsync(forum, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { updatedSection }.AsReadOnly());

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAllForumsAsync_AllForumsFail_CompletesWithoutThrowing()
    {
        var forum1 = CreateForum(1, "Fail1");
        var forum2 = CreateForum(2, "Fail2");
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum1, forum2 }.AsReadOnly());

        _sectionRepository.GetByForumIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        _discoveryService.DiscoverSectionsAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Total failure"));

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAllForumsAsync_SkipsInactiveSectionsInDelta()
    {
        var forum = CreateForum();
        _forumRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Forum> { forum }.AsReadOnly());

        // Existing inactive section should not count in delta
        var inactiveSection = CreateSection(1, forum.ForumId, "https://forum1.example.com/inactive", "Inactive", isActive: false);
        _sectionRepository.GetByForumIdAsync(forum.ForumId, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection> { inactiveSection }.AsReadOnly());

        _discoveryService.DiscoverSectionsAsync(forum, Arg.Any<CancellationToken>())
            .Returns(new List<ForumSection>().AsReadOnly());

        var service = CreateService();

        var act = async () => await service.RefreshAllForumsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // --- GetRefreshIntervalAsync tests ---

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsConfiguredValue()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .Returns("12");

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(12);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenNoSetting()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(ForumStructureRefreshService.DefaultIntervalHours);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenInvalidSetting()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .Returns("not_a_number");

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(ForumStructureRefreshService.DefaultIntervalHours);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenNegativeValue()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .Returns("-5");

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(ForumStructureRefreshService.DefaultIntervalHours);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenZeroValue()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .Returns("0");

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(ForumStructureRefreshService.DefaultIntervalHours);
    }

    [Fact]
    public async Task GetRefreshIntervalAsync_ReturnsDefault_WhenRepositoryThrows()
    {
        _settingRepository.GetValueAsync(ForumStructureRefreshService.SettingKey, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var service = CreateService();
        var result = await service.GetRefreshIntervalAsync(CancellationToken.None);

        result.Should().Be(ForumStructureRefreshService.DefaultIntervalHours);
    }

    // --- Helper ---

    private ForumStructureRefreshService CreateService()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _forumRepository);
        services.AddScoped(_ => _discoveryService);
        services.AddScoped(_ => _sectionRepository);
        services.AddScoped(_ => _settingRepository);

        var provider = services.BuildServiceProvider();
        return new ForumStructureRefreshService(
            provider,
            NullLogger<ForumStructureRefreshService>.Instance);
    }
}
