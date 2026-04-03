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
    private readonly ILogger<PlaywrightAuthenticator> _logger = Substitute.For<ILogger<PlaywrightAuthenticator>>();

    public async ValueTask DisposeAsync()
    {
        // No resources to clean up in unit tests with mocked Playwright
        await ValueTask.CompletedTask;
    }

    // ── Constructor Validation ─────────────────────────────────────

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new PlaywrightAuthenticator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ValidLogger_DoesNotThrow()
    {
        var act = () => new PlaywrightAuthenticator(_logger);
        act.Should().NotThrow();
    }

    // ── AuthenticateAsync Input Validation ──────────────────────────

    [Fact]
    public async Task AuthenticateAsync_NullLoginUrl_ThrowsArgumentException()
    {
        var sut = new PlaywrightAuthenticator(_logger);
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync(null!, credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyLoginUrl_ThrowsArgumentException()
    {
        var sut = new PlaywrightAuthenticator(_logger);
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("", credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_WhitespaceLoginUrl_ThrowsArgumentException()
    {
        var sut = new PlaywrightAuthenticator(_logger);
        var credentials = new ForumCredentials { Username = "user", Password = "pass" };

        var act = () => sut.AuthenticateAsync("   ", credentials);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AuthenticateAsync_NullCredentials_ThrowsArgumentNullException()
    {
        var sut = new PlaywrightAuthenticator(_logger);

        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AuthenticateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var sut = new PlaywrightAuthenticator(_logger);
        await sut.DisposeAsync();

        var credentials = new ForumCredentials { Username = "user", Password = "pass" };
        var act = () => sut.AuthenticateAsync("https://forum.example.com/login.php", credentials);

        await act.Should().ThrowAsync<ObjectDisposedException>();
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
        var sut = new PlaywrightAuthenticator(_logger);
        await sut.DisposeAsync();

        var act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
