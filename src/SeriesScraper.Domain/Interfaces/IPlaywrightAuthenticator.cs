using System.Net;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Authenticates against a forum using a headless browser (Playwright).
/// Required for forums that use reCAPTCHA v3 on the login form,
/// where a standard HTTP POST cannot supply the g-recaptcha-response token.
/// The headless browser loads the login page, executes the site's reCAPTCHA
/// script automatically, then submits the form.
/// </summary>
public interface IPlaywrightAuthenticator : IAsyncDisposable
{
    /// <summary>
    /// Authenticates by driving a headless browser to the forum login page,
    /// filling in credentials, and submitting the form.
    /// Returns the session cookies (especially warforum_sid) on success.
    /// </summary>
    /// <param name="loginUrl">Full URL to the forum login page.</param>
    /// <param name="credentials">Forum username and password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CookieContainer with the authenticated session cookies.</returns>
    /// <exception cref="InvalidOperationException">Thrown when browser login fails.</exception>
    Task<CookieContainer> AuthenticateAsync(
        string loginUrl,
        ForumCredentials credentials,
        CancellationToken cancellationToken = default);
}
