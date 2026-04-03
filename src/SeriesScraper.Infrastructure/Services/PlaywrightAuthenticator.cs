using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Authenticates against a forum by driving a headless Chromium browser via Playwright.
/// Solves the reCAPTCHA v3 problem: the site's script auto-generates the
/// g-recaptcha-response token when the form is submitted in a real browser.
/// After login, session cookies are extracted and returned for use with HttpClient.
/// The browser is disposed immediately after authentication.
/// </summary>
public sealed class PlaywrightAuthenticator : IPlaywrightAuthenticator
{
    private readonly ILogger<PlaywrightAuthenticator> _logger;

    // Selector constants for the phpBB2 login form
    internal const string UsernameSelector = "input[name='username']";
    internal const string PasswordSelector = "input[name='password']";
    internal const string SubmitSelector = "input[name='login'], input[type='submit']";
    internal const string SessionCookieName = "warforum_sid";

    // Timeouts
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PostLoginWait = TimeSpan.FromSeconds(5);

    private IPlaywright? _playwright;
    private bool _disposed;

    public PlaywrightAuthenticator(ILogger<PlaywrightAuthenticator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CookieContainer> AuthenticateAsync(
        string loginUrl,
        ForumCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginUrl);
        ArgumentNullException.ThrowIfNull(credentials);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation(
            "Starting Playwright authentication for {LoginUrl} as {Username}",
            loginUrl, credentials.Username);

        _playwright ??= await Playwright.CreateAsync();

        IBrowser? browser = null;
        try
        {
            browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            page.SetDefaultNavigationTimeout((float)NavigationTimeout.TotalMilliseconds);

            // Navigate to login page
            _logger.LogDebug("Navigating to {LoginUrl}", loginUrl);
            var response = await page.GotoAsync(loginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            if (response is null || !response.Ok)
            {
                var status = response?.Status ?? 0;
                throw new InvalidOperationException(
                    $"Failed to load login page '{loginUrl}' — HTTP {status}.");
            }

            // Fill in the login form
            _logger.LogDebug("Filling login form for user {Username}", credentials.Username);

            await page.FillAsync(UsernameSelector, credentials.Username);
            await page.FillAsync(PasswordSelector, credentials.Password);

            // Submit the form — reCAPTCHA v3 token is generated automatically by the site's script
            _logger.LogDebug("Submitting login form (reCAPTCHA v3 will auto-execute)");
            await page.ClickAsync(SubmitSelector);

            // Wait for navigation after form submission
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Navigation after login timed out — checking cookies anyway");
            }

            // Brief wait for any async cookie setting
            await Task.Delay(PostLoginWait, cancellationToken);

            // Extract cookies
            var playwrightCookies = await context.CookiesAsync();
            var cookieContainer = new CookieContainer();
            var hasSessionCookie = false;

            foreach (var cookie in playwrightCookies)
            {
                try
                {
                    var netCookie = new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
                    if (cookie.Expires > 0)
                    {
                        netCookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)cookie.Expires).UtcDateTime;
                    }
                    netCookie.Secure = cookie.Secure;
                    netCookie.HttpOnly = cookie.HttpOnly;
                    cookieContainer.Add(netCookie);

                    if (cookie.Name.Equals(SessionCookieName, StringComparison.OrdinalIgnoreCase))
                    {
                        hasSessionCookie = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert cookie '{CookieName}' — skipping", cookie.Name);
                }
            }

            if (!hasSessionCookie)
            {
                _logger.LogWarning(
                    "Session cookie '{SessionCookieName}' not found after login — authentication may have failed. " +
                    "Cookies found: {CookieNames}",
                    SessionCookieName,
                    string.Join(", ", playwrightCookies.Select(c => c.Name)));

                throw new InvalidOperationException(
                    $"Playwright login completed but session cookie '{SessionCookieName}' was not set. " +
                    "The login may have failed or the reCAPTCHA challenge was not solved.");
            }

            _logger.LogInformation(
                "Playwright authentication succeeded — {CookieCount} cookies extracted (session cookie present)",
                playwrightCookies.Count);

            return cookieContainer;
        }
        finally
        {
            if (browser is not null)
            {
                await browser.CloseAsync();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_playwright is not null)
        {
            _playwright.Dispose();
            _playwright = null;
        }

        await ValueTask.CompletedTask;
    }
}
