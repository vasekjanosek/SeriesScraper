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
}
