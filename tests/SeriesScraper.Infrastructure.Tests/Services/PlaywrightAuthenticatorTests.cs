using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class PlaywrightAuthenticatorTests : IAsyncDisposable
{
    private readonly ILogger<PlaywrightAuthenticator> _logger =
        Substitute.For<ILogger<PlaywrightAuthenticator>>();

    private readonly IPlaywrightFactory _factory =
        Substitute.For<IPlaywrightFactory>();

    // Playwright browser mock chain used by auth-flow tests
    private readonly IPlaywright _playwright = Substitute.For<IPlaywright>();
    private readonly IBrowserType _browserType = Substitute.For<IBrowserType>();
    private readonly IBrowser _browser = Substitute.For<IBrowser>();
    private readonly IBrowserContext _context = Substitute.For<IBrowserContext>();
    private readonly IPage _page = Substitute.For<IPage>();
    private readonly IResponse _response = Substitute.For<IResponse>();

    public PlaywrightAuthenticatorTests()
    {
        // Wire up the happy-path mock chain (individual tests override what they need)
        _factory.CreateAsync().Returns(_playwright);
        _playwright.Chromium.Returns(_browserType);
        _browserType.LaunchAsync(Arg.Any<BrowserTypeLaunchOptions>()).Returns(_browser);
        _browser.NewContextAsync(Arg.Any<BrowserNewContextOptions?>()).Returns(_context);
        _context.NewPageAsync().Returns(_page);
        _response.Ok.Returns(true);
        _response.Status.Returns(200);
        _page.GotoAsync(Arg.Any<string>(), Arg.Any<PageGotoOptions?>()).Returns(_response);
        _page.FillAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PageFillOptions?>()).Returns(Task.CompletedTask);
        _page.ClickAsync(Arg.Any<string>(), Arg.Any<PageClickOptions?>()).Returns(Task.CompletedTask);
        _page.WaitForLoadStateAsync(Arg.Any<LoadState?>(), Arg.Any<PageWaitForLoadStateOptions?>()).Returns(Task.CompletedTask);
        _browser.CloseAsync().Returns(Task.CompletedTask);
    }

    public async ValueTask DisposeAsync()
    {
        await ValueTask.CompletedTask;
    }

    /// Creates a sut with TimeSpan.Zero post-login wait so auth tests run instantly.
    private PlaywrightAuthenticator CreateSut() =>
        new(_logger, _factory, TimeSpan.Zero);

    // ── Constructor Validation ─────────────────────────────────────

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new PlaywrightAuthenticator(null!, _factory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => new PlaywrightAuthenticator(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("playwrightFactory");
    }

    [Fact]
    public void Constructor_ValidArguments_DoesNotThrow()
    {
        var act = () => new PlaywrightAuthenticator(_logger, _factory);
        act.Should().NotThrow();
    }

    // ── AuthenticateAsync Input Validation ──────────────────────────

    [Fact]
    public async Task AuthenticateAsync_NullLoginUrl_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync(null!, credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyLoginUrl_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("", credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WhitespaceLoginUrl_ThrowsArgumentException()
    {
        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("   ", credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_NullCredentials_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AuthenticateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var sut = CreateSut();
        await sut.DisposeAsync();

        var credentials = new ForumCredentials { Username = "user", Password = "pass" };
        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ── Auth Flow — Successful Login ────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_SuccessfulLogin_ReturnsCookiesWithSessionCookie()
    {
        // Arrange
        var sessionCookie = new BrowserContextCookiesResult
        {
            Name = "warforum_sid",
            Value = "abc123session",
            Path = "/",
            Domain = "forum.example.com",
            Secure = false,
            HttpOnly = true,
            Expires = -1
        };

        _context.CookiesAsync()
            .Returns(new List<BrowserContextCookiesResult> { sessionCookie });

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        // Act
        var result = await sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Assert
        result.Should().NotBeNull();
        result.GetCookies(new Uri("https://forum.example.com/")).Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuthenticateAsync_SuccessfulLogin_DisposesPlaywrightAndBrowserAfterCall()
    {
        // Arrange
        _context.CookiesAsync()
            .Returns(new List<BrowserContextCookiesResult>
            {
                new() { Name = "warforum_sid", Value = "s", Path = "/", Domain = "forum.example.com" }
            });

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        // Act
        await sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Assert — per-call playwright must be disposed and browser closed
        _playwright.Received(1).Dispose();
        await _browser.Received(1).CloseAsync();
    }

    // ── Auth Flow — Login Page Fails to Load ───────────────────────

    [Fact]
    public async Task AuthenticateAsync_LoginPageReturnsHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        _response.Ok.Returns(false);
        _response.Status.Returns(403);

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Act & Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to load login page*");
    }

    [Fact]
    public async Task AuthenticateAsync_LoginPageReturnsNullResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        _page.GotoAsync(Arg.Any<string>(), Arg.Any<PageGotoOptions?>())
            .Returns((IResponse?)null);

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Act & Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to load login page*");
    }

    // ── Auth Flow — No Session Cookie After Login ──────────────────

    [Fact]
    public async Task AuthenticateAsync_NoSessionCookieAfterLogin_ThrowsInvalidOperationException()
    {
        // Arrange — cookies returned but warforum_sid is absent
        _context.CookiesAsync()
            .Returns(new List<BrowserContextCookiesResult>
            {
                new() { Name = "phpbb_data", Value = "x", Path = "/", Domain = "forum.example.com" }
            });

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Act & Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*session cookie*");
    }

    // ── Auth Flow — Browser Launch Failure ────────────────────────

    [Fact]
    public async Task AuthenticateAsync_BrowserLaunchThrows_PropagatesExceptionAndDisposesPlaywright()
    {
        // Arrange
        var launchError = new InvalidOperationException("Browser binary not found");
        _browserType.LaunchAsync(Arg.Any<BrowserTypeLaunchOptions>())
            .ThrowsAsync(launchError);

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Act & Assert — exception propagates; playwright is still disposed in finally
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Browser binary not found");
        _playwright.Received(1).Dispose();
    }

    // ── Auth Flow — Cookie with Illegal Name Characters ────────────

    [Fact]
    public async Task AuthenticateAsync_CookieWithIllegalName_IsSkippedAndSessionCookieStillExtracted()
    {
        // Arrange — first cookie has CR/LF in name (illegal), second is the valid session cookie
        _context.CookiesAsync()
            .Returns(new List<BrowserContextCookiesResult>
            {
                new() { Name = "bad\r\ncookie", Value = "x", Path = "/", Domain = "forum.example.com" },
                new() { Name = "warforum_sid", Value = "good123", Path = "/", Domain = "forum.example.com" }
            });

        var sut = CreateSut();
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        // Act — should not throw; bad cookie is silently skipped
        var result = await sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        // Assert
        result.Should().NotBeNull();
        result.GetCookies(new Uri("https://forum.example.com/"))
            .Should().Contain(c => c.Name == "warforum_sid");
    }

    // ── Constants Verification ──────────────────────────────────────

    [Fact]
    public void UsernameSelector_IsCorrectCssSelector()
    {
        PlaywrightAuthenticator.UsernameSelector.Should().Be("input[name='username']");
    }

    [Fact]
    public void PasswordSelector_IsCorrectCssSelector()
    {
        PlaywrightAuthenticator.PasswordSelector.Should().Be("input[name='password']");
    }

    [Fact]
    public void SubmitSelector_IsCorrectCssSelector()
    {
        PlaywrightAuthenticator.SubmitSelector.Should().Be("input[name='login'], input[type='submit']");
    }

    [Fact]
    public void SessionCookieName_IsWarforumSid()
    {
        PlaywrightAuthenticator.SessionCookieName.Should().Be("warforum_sid");
    }

    // ── Dispose ─────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut();
        await sut.DisposeAsync();

        var act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
