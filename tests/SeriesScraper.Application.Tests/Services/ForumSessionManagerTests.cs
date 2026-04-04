using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Tests.Services;

public class ForumSessionManagerTests : IDisposable
{
    private readonly IForumScraper _forumScraper = Substitute.For<IForumScraper>();
    private readonly IForumCredentialService _credentialService = Substitute.For<IForumCredentialService>();
    private readonly ILogger<ForumSessionManager> _logger = Substitute.For<ILogger<ForumSessionManager>>();
    private readonly ForumSessionManager _sut;

    private static Forum CreateForum(int id = 1, string name = "TestForum") => new()
    {
        ForumId = id,
        Name = name,
        BaseUrl = "https://forum.example.com",
        Username = "testuser",
        CredentialKey = "FORUM_TEST_PASSWORD"
    };

    public ForumSessionManagerTests()
    {
        _forumScraper.GetCookieContainer().Returns(_ => new CookieContainer());
        _sut = new ForumSessionManager(_forumScraper, _credentialService, _logger);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor Validation ─────────────────────────────────────

    [Fact]
    public void Constructor_NullForumScraper_ThrowsArgumentNullException()
    {
        var act = () => new ForumSessionManager(null!, _credentialService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("forumScraper");
    }

    [Fact]
    public void Constructor_NullCredentialService_ThrowsArgumentNullException()
    {
        var act = () => new ForumSessionManager(_forumScraper, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("credentialService");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ForumSessionManager(_forumScraper, _credentialService, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── GetAuthenticatedClientAsync ────────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_SuccessfulLogin_ReturnsHttpClient()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = await _sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();
        client.BaseAddress.Should().Be(new Uri("https://forum.example.com"));
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_CalledTwice_ReusesSameSession()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client1 = await _sut.GetAuthenticatedClientAsync(forum);
        var client2 = await _sut.GetAuthenticatedClientAsync(forum);

        client1.Should().BeSameAs(client2);
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_CredentialNotSet_ThrowsInvalidOperationException()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns((string?)null);

        var act = () => _sut.GetAuthenticatedClientAsync(forum);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not set*");
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_AuthenticationFails_ThrowsInvalidOperationException()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => _sut.GetAuthenticatedClientAsync(forum);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Authentication failed*");
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_NullForum_ThrowsArgumentNullException()
    {
        var act = () => _sut.GetAuthenticatedClientAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_PassesCorrectCredentials()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("my-password");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.GetAuthenticatedClientAsync(forum);

        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Is<ForumCredentials>(c => c.Username == "testuser" && c.Password == "my-password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_DifferentForums_CreatesSeparateSessions()
    {
        var forum1 = CreateForum(id: 1, name: "Forum1");
        var forum2 = CreateForum(id: 2, name: "Forum2");
        forum2.BaseUrl = "https://forum2.example.com";
        forum2.CredentialKey = "FORUM_SECOND_PASSWORD";

        _credentialService.ResolveCredential(Arg.Any<string>()).Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client1 = await _sut.GetAuthenticatedClientAsync(forum1);
        var client2 = await _sut.GetAuthenticatedClientAsync(forum2);

        client1.Should().NotBeSameAs(client2);
        client1.BaseAddress.Should().Be(new Uri("https://forum.example.com"));
        client2.BaseAddress.Should().Be(new Uri("https://forum2.example.com"));
        await _forumScraper.Received(2).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ExpiredSession_ReAuthenticates()
    {
        // Use a very short session duration so it expires immediately
        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            TimeSpan.FromMilliseconds(1));

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client1 = await sut.GetAuthenticatedClientAsync(forum);

        // Wait for session to expire
        await Task.Delay(50);

        var client2 = await sut.GetAuthenticatedClientAsync(forum);

        client1.Should().NotBeSameAs(client2);
        await _forumScraper.Received(2).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var forum = CreateForum();
        _sut.Dispose();

        var act = () => _sut.GetAuthenticatedClientAsync(forum);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.GetAuthenticatedClientAsync(forum, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── IsSessionValid ──────────────────────────────────────────────

    [Fact]
    public void IsSessionValid_NoSession_ReturnsFalse()
    {
        _sut.IsSessionValid(999).Should().BeFalse();
    }

    [Fact]
    public async Task IsSessionValid_AfterSuccessfulLogin_ReturnsTrue()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.GetAuthenticatedClientAsync(forum);

        _sut.IsSessionValid(forum.ForumId).Should().BeTrue();
    }

    [Fact]
    public async Task IsSessionValid_ExpiredSession_ReturnsFalse()
    {
        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            TimeSpan.FromMilliseconds(1));

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await sut.GetAuthenticatedClientAsync(forum);
        await Task.Delay(50);

        sut.IsSessionValid(forum.ForumId).Should().BeFalse();
    }

    [Fact]
    public void IsSessionValid_AfterDispose_ThrowsObjectDisposedException()
    {
        _sut.Dispose();

        var act = () => _sut.IsSessionValid(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    // ── RefreshSessionAsync ─────────────────────────────────────────

    [Fact]
    public async Task RefreshSessionAsync_EstablishesNewSession()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Establish initial session
        var client1 = await _sut.GetAuthenticatedClientAsync(forum);

        // Refresh
        await _sut.RefreshSessionAsync(forum);

        // Get new client (should be different from the old one since old was disposed)
        var client2 = await _sut.GetAuthenticatedClientAsync(forum);

        // Authentication called 3 times: initial + refresh + get after refresh
        // Actually: initial(1) + refresh(2) + get reuses refresh session(2 total calls to Authenticate)
        // Refresh calls Authenticate, then GetAuthenticated reuses.
        await _forumScraper.Received(2).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshSessionAsync_InvalidatesOldSession()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client1 = await _sut.GetAuthenticatedClientAsync(forum);
        await _sut.RefreshSessionAsync(forum);
        var client2 = await _sut.GetAuthenticatedClientAsync(forum);

        // Old client should have been replaced
        client1.Should().NotBeSameAs(client2);
    }

    [Fact]
    public async Task RefreshSessionAsync_NullForum_ThrowsArgumentNullException()
    {
        var act = () => _sut.RefreshSessionAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RefreshSessionAsync_AuthenticationFails_ThrowsInvalidOperationException()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => _sut.RefreshSessionAsync(forum);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RefreshSessionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var forum = CreateForum();
        _sut.Dispose();

        var act = () => _sut.RefreshSessionAsync(forum);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ── InvalidateSession ───────────────────────────────────────────

    [Fact]
    public async Task InvalidateSession_RemovesSession()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.GetAuthenticatedClientAsync(forum);
        _sut.IsSessionValid(forum.ForumId).Should().BeTrue();

        _sut.InvalidateSession(forum.ForumId);

        _sut.IsSessionValid(forum.ForumId).Should().BeFalse();
    }

    [Fact]
    public void InvalidateSession_NonexistentSession_DoesNotThrow()
    {
        var act = () => _sut.InvalidateSession(999);

        act.Should().NotThrow();
    }

    [Fact]
    public void InvalidateSession_AfterDispose_ThrowsObjectDisposedException()
    {
        _sut.Dispose();

        var act = () => _sut.InvalidateSession(1);

        act.Should().Throw<ObjectDisposedException>();
    }

    // ── Dispose ─────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var act = () =>
        {
            _sut.Dispose();
            _sut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_CleansUpClients()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.GetAuthenticatedClientAsync(forum);

        _sut.Dispose();

        // After dispose, GetAuthenticatedClientAsync should throw
        var act = () => _sut.GetAuthenticatedClientAsync(forum);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ── Concurrent Access ───────────────────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_ConcurrentCalls_OnlyAuthenticatesOnce()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        // Add a delay to simulate slow authentication
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(100);
                return true;
            });

        // Fire multiple concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _sut.GetAuthenticatedClientAsync(forum))
            .ToArray();

        var clients = await Task.WhenAll(tasks);

        // All should return the same client
        clients.Should().AllSatisfy(c => c.Should().BeSameAs(clients[0]));

        // Authentication should only be called once due to double-check locking
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ConcurrentDifferentForums_AuthenticatesBoth()
    {
        var forum1 = CreateForum(id: 1);
        var forum2 = CreateForum(id: 2);
        forum2.BaseUrl = "https://forum2.example.com";
        forum2.CredentialKey = "FORUM_SECOND_PASSWORD";

        _credentialService.ResolveCredential(Arg.Any<string>()).Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var task1 = _sut.GetAuthenticatedClientAsync(forum1);
        var task2 = _sut.GetAuthenticatedClientAsync(forum2);

        var results = await Task.WhenAll(task1, task2);

        results[0].Should().NotBeSameAs(results[1]);
        await _forumScraper.Received(2).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    // ── Custom Session Duration ─────────────────────────────────────

    [Fact]
    public async Task CustomSessionDuration_SessionExpiresAfterSpecifiedDuration()
    {
        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            TimeSpan.FromMilliseconds(50));

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await sut.GetAuthenticatedClientAsync(forum);
        sut.IsSessionValid(forum.ForumId).Should().BeTrue();

        await Task.Delay(100);

        sut.IsSessionValid(forum.ForumId).Should().BeFalse();
    }

    // ── Cookie Transfer ─────────────────────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_UsesCookiesFromForumScraper()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        var scraperCookies = new CookieContainer();
        scraperCookies.Add(new Uri("https://forum.example.com"), new Cookie("session_id", "abc123"));
        _forumScraper.GetCookieContainer().Returns(scraperCookies);
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = await _sut.GetAuthenticatedClientAsync(forum);

        // The returned client should have the scraper's cookies
        _forumScraper.Received().GetCookieContainer();
        client.Should().NotBeNull();
    }

    // ── Age Verification (#88) ──────────────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_AttemptsAgeVerificationAfterLogin()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Age verification will fail (no real server), but is non-fatal
        await _sut.GetAuthenticatedClientAsync(forum);

        // Verify that age verification was attempted by checking logger was called
        // with the age verification debug message
        _logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("age verification", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_AgeVerificationFailure_DoesNotPreventLogin()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Authentication should succeed even though age verification fails (no server)
        var client = await _sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();
        _sut.IsSessionValid(forum.ForumId).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshSessionAsync_AlsoAttemptsAgeVerification()
    {
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.GetAuthenticatedClientAsync(forum);

        // Clear received calls
        _logger.ClearReceivedCalls();

        await _sut.RefreshSessionAsync(forum);

        // Age verification attempted again during refresh
        _logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("age verification", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Playwright Authentication (#89) ─────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_PlaywrightAvailable_UsesPlaywrightFirst()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        var cookieContainer = new CookieContainer();
        cookieContainer.Add(new Uri("https://forum.example.com"), new System.Net.Cookie("warforum_sid", "test123"));

        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(cookieContainer);

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        var client = await sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();
        client.BaseAddress.Should().Be(new Uri("https://forum.example.com"));

        // Playwright was called
        await playwrightAuth.Received(1).AuthenticateAsync(
            "https://forum.example.com/login.php",
            Arg.Is<ForumCredentials>(c => c.Username == "testuser" && c.Password == "secret"),
            Arg.Any<CancellationToken>());

        // IForumScraper was NOT called (Playwright succeeded)
        await _forumScraper.DidNotReceive().AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_PlaywrightFails_FallsBackToForumScraper()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Playwright browser failed"));

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = await sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();

        // Playwright was called first
        await playwrightAuth.Received(1).AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());

        // IForumScraper was called as fallback
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_PlaywrightFailsWithCancellation_DoesNotFallBack()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Cancelled"));

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        var act = () => sut.GetAuthenticatedClientAsync(forum);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // IForumScraper should NOT be called — cancellation should propagate
        await _forumScraper.DidNotReceive().AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_PlaywrightConstructsCorrectLoginUrl()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new CookieContainer());

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth);

        var forum = CreateForum();
        forum.BaseUrl = "https://warforum.xyz/";
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        await sut.GetAuthenticatedClientAsync(forum);

        await playwrightAuth.Received(1).AuthenticateAsync(
            "https://warforum.xyz/login.php",
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_NoPlaywrightNoManualCookie_UsesForumScraper()
    {
        // Default _sut has no Playwright and no SettingRepository
        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = await _sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    // ── Manual Cookie Injection (#89) ───────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookieSet_UsesManualCookie()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("warforum_sid=manual123; warforum_data=xyz");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();

        var client = await sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();
        client.BaseAddress.Should().Be(new Uri("https://forum.example.com"));

        // No Playwright or ForumScraper auth should be called
        await _forumScraper.DidNotReceive().AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());

        // Credential resolution not needed for manual cookie
        _credentialService.DidNotReceive().ResolveCredential(Arg.Any<string>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookieEmpty_FallsThrough()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var client = await sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();

        // Empty manual cookie → falls through to IForumScraper
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookieNull_FallsThrough()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await sut.GetAuthenticatedClientAsync(forum);

        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookiePrioritizedOverPlaywright()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("warforum_sid=fromSettings");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth,
            settingRepository: settingRepo);

        var forum = CreateForum();

        var client = await sut.GetAuthenticatedClientAsync(forum);

        client.Should().NotBeNull();

        // Manual cookie takes priority — neither Playwright nor ForumScraper called
        await playwrightAuth.DidNotReceive().AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
        await _forumScraper.DidNotReceive().AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookieUsesCorrectSettingKey()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum(id: 42);
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await sut.GetAuthenticatedClientAsync(forum);

        // Verify the correct setting key was queried
        await settingRepo.Received(1).GetValueAsync("forum.42.session_cookie", Arg.Any<CancellationToken>());
    }

    // ── ManualCookieSettingPrefix/Suffix Constants ──────────────────

    [Fact]
    public void ManualCookieSettingPrefix_IsCorrect()
    {
        ForumSessionManager.ManualCookieSettingPrefix.Should().Be("forum.");
    }

    [Fact]
    public void ManualCookieSettingSuffix_IsCorrect()
    {
        ForumSessionManager.ManualCookieSettingSuffix.Should().Be(".session_cookie");
    }

    // ── Full Auth Strategy Chain ────────────────────────────────────

    [Fact]
    public async Task GetAuthenticatedClientAsync_AllStrategiesFail_ThrowsFromForumScraper()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Playwright failed"));

        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth,
            settingRepository: settingRepo);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => sut.GetAuthenticatedClientAsync(forum);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Authentication failed*");
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_PlaywrightSucceeds_SessionIsValid()
    {
        var playwrightAuth = Substitute.For<IPlaywrightAuthenticator>();
        playwrightAuth.AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new CookieContainer());

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            playwrightAuthenticator: playwrightAuth);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");

        await sut.GetAuthenticatedClientAsync(forum);

        sut.IsSessionValid(forum.ForumId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookie_SessionIsValid()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("warforum_sid=test");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();

        await sut.GetAuthenticatedClientAsync(forum);

        sut.IsSessionValid(forum.ForumId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_ManualCookieReusesSession()
    {
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("warforum_sid=test");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();

        var client1 = await sut.GetAuthenticatedClientAsync(forum);
        var client2 = await sut.GetAuthenticatedClientAsync(forum);

        client1.Should().BeSameAs(client2);

        // Setting repo only queried once (for the initial auth, not for reuse)
        await settingRepo.Received(1).GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthenticatedClientAsync_InvalidCookieNameWithSpace_LogsWarningAndDoesNotThrow()
    {
        // Arrange — a cookie name starting with '$' is illegal per RFC 6265 and throws CookieException.
        // Note: .NET 8 allows spaces in cookie names but still rejects '$'-prefixed names.
        var settingRepo = Substitute.For<ISettingRepository>();
        settingRepo.GetValueAsync("forum.1.session_cookie", Arg.Any<CancellationToken>())
            .Returns("$invalid_cookie=value");

        using var sut = new ForumSessionManager(
            _forumScraper, _credentialService, _logger,
            settingRepository: settingRepo);

        var forum = CreateForum();
        _credentialService.ResolveCredential("FORUM_TEST_PASSWORD").Returns("secret");
        _forumScraper.AuthenticateAsync(Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act — must not propagate CookieException
        var act = () => sut.GetAuthenticatedClientAsync(forum);
        await act.Should().NotThrowAsync();

        // Assert — a warning was logged about the skipped invalid cookie
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                             || o.ToString()!.Contains("Skipping", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CookieException?>(),
            Arg.Any<Func<object, Exception?, string>>());

        // Assert — all cookies were malformed so auth fell through to IForumScraper
        await _forumScraper.Received(1).AuthenticateAsync(
            Arg.Any<ForumCredentials>(), Arg.Any<CancellationToken>());
    }
}
