using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Manages authenticated forum sessions with cookie-based persistence.
/// Thread-safe: uses SemaphoreSlim per forum to serialize authentication
/// while allowing concurrent reads of established sessions.
/// Authentication strategy order:
///   1. Manual cookie injection (setting key "forum.{id}.session_cookie")
///   2. Playwright headless browser (handles reCAPTCHA v3)
///   3. IForumScraper.AuthenticateAsync (fallback for forums without reCAPTCHA)
/// </summary>
public sealed class ForumSessionManager : IForumSessionManager
{
    private readonly IForumScraper _forumScraper;
    private readonly IForumCredentialService _credentialService;
    private readonly ILogger<ForumSessionManager> _logger;
    private readonly IPlaywrightAuthenticator? _playwrightAuthenticator;
    private readonly ISettingRepository? _settingRepository;

    private readonly ConcurrentDictionary<int, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<int, HttpClient> _clients = new();

    private readonly TimeSpan _defaultSessionDuration;
    private bool _disposed;

    /// <summary>
    /// Setting key prefix for manual session cookie injection.
    /// Full key format: "forum.{forumId}.session_cookie"
    /// </summary>
    internal const string ManualCookieSettingPrefix = "forum.";
    internal const string ManualCookieSettingSuffix = ".session_cookie";

    /// <summary>
    /// Creates a new ForumSessionManager.
    /// </summary>
    /// <param name="forumScraper">Forum scraper for authentication.</param>
    /// <param name="credentialService">Service to resolve credentials from environment variables.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="defaultSessionDuration">
    /// Default session duration when no explicit expiry is known.
    /// If null, defaults to 30 minutes.
    /// </param>
    /// <param name="playwrightAuthenticator">Optional Playwright authenticator for reCAPTCHA-protected forums.</param>
    /// <param name="settingRepository">Optional setting repository for manual cookie injection.</param>
    public ForumSessionManager(
        IForumScraper forumScraper,
        IForumCredentialService credentialService,
        ILogger<ForumSessionManager> logger,
        TimeSpan? defaultSessionDuration = null,
        IPlaywrightAuthenticator? playwrightAuthenticator = null,
        ISettingRepository? settingRepository = null)
    {
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultSessionDuration = defaultSessionDuration ?? TimeSpan.FromMinutes(30);
        _playwrightAuthenticator = playwrightAuthenticator;
        _settingRepository = settingRepository;
    }

    /// <inheritdoc />
    public async Task<HttpClient> GetAuthenticatedClientAsync(Forum forum, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forum);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: valid session exists
        if (_sessions.TryGetValue(forum.ForumId, out var session) && !session.IsExpired)
        {
            _logger.LogDebug("Reusing existing session for forum {ForumId} ({ForumName})", forum.ForumId, forum.Name);
            return _clients[forum.ForumId];
        }

        // Slow path: need to authenticate (serialized per forum)
        var semaphore = _locks.GetOrAdd(forum.ForumId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock — another thread may have authenticated
            if (_sessions.TryGetValue(forum.ForumId, out session) && !session.IsExpired)
            {
                _logger.LogDebug("Session established by another thread for forum {ForumId}", forum.ForumId);
                return _clients[forum.ForumId];
            }

            await AuthenticateInternalAsync(forum, cancellationToken);
            return _clients[forum.ForumId];
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public bool IsSessionValid(int forumId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessions.TryGetValue(forumId, out var session) && !session.IsExpired;
    }

    /// <inheritdoc />
    public async Task RefreshSessionAsync(Forum forum, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forum);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var semaphore = _locks.GetOrAdd(forum.ForumId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Refreshing session for forum {ForumId} ({ForumName})", forum.ForumId, forum.Name);
            CleanupForumSession(forum.ForumId);
            await AuthenticateInternalAsync(forum, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public void InvalidateSession(int forumId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Invalidating session for forum {ForumId}", forumId);
        CleanupForumSession(forumId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();

        foreach (var semaphore in _locks.Values)
        {
            semaphore.Dispose();
        }
        _locks.Clear();

        _sessions.Clear();
    }

    private async Task AuthenticateInternalAsync(Forum forum, CancellationToken cancellationToken)
    {
        // Strategy 1: Manual cookie injection (operator-provided session cookie)
        var manualCookie = await TryGetManualCookieAsync(forum.ForumId, forum.BaseUrl, cancellationToken);
        if (manualCookie is not null)
        {
            _logger.LogInformation(
                "Using manually injected session cookie for forum {ForumId} ({ForumName})",
                forum.ForumId, forum.Name);
            EstablishSession(forum, manualCookie);
            await DismissAgeVerificationAsync(_clients[forum.ForumId], forum, cancellationToken);
            return;
        }

        // Resolve credentials (needed for both Playwright and IForumScraper fallback)
        var password = _credentialService.ResolveCredential(forum.CredentialKey);
        if (password is null)
        {
            _logger.LogError(
                "Credential environment variable {CredentialKey} not set for forum {ForumId} ({ForumName})",
                forum.CredentialKey, forum.ForumId, forum.Name);
            throw new InvalidOperationException(
                $"Credential environment variable '{forum.CredentialKey}' is not set for forum '{forum.Name}'.");
        }

        var credentials = new ForumCredentials
        {
            Username = forum.Username,
            Password = password
        };

        // Strategy 2: Playwright headless browser (handles reCAPTCHA v3)
        if (_playwrightAuthenticator is not null)
        {
            try
            {
                _logger.LogInformation(
                    "Attempting Playwright authentication for forum {ForumId} ({ForumName}) as {Username}",
                    forum.ForumId, forum.Name, forum.Username);

                var loginUrl = forum.BaseUrl.TrimEnd('/') + "/login.php";
                var cookieContainer = await _playwrightAuthenticator.AuthenticateAsync(
                    loginUrl, credentials, cancellationToken);

                EstablishSession(forum, cookieContainer);
                await DismissAgeVerificationAsync(_clients[forum.ForumId], forum, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Playwright authentication failed for forum {ForumId} ({ForumName}) — falling back to IForumScraper",
                    forum.ForumId, forum.Name);
            }
        }

        // Strategy 3: IForumScraper fallback (direct HTTP login, no reCAPTCHA support)
        _logger.LogInformation(
            "Authenticating to forum {ForumId} ({ForumName}) as {Username} via IForumScraper",
            forum.ForumId, forum.Name, forum.Username);

        var success = await _forumScraper.AuthenticateAsync(credentials, cancellationToken);
        if (!success)
        {
            _logger.LogWarning(
                "Authentication failed for forum {ForumId} ({ForumName}) as {Username}",
                forum.ForumId, forum.Name, forum.Username);
            throw new InvalidOperationException(
                $"Authentication failed for forum '{forum.Name}' with username '{forum.Username}'.");
        }

        var scraperCookies = _forumScraper.GetCookieContainer();
        EstablishSession(forum, scraperCookies);
        await DismissAgeVerificationAsync(_clients[forum.ForumId], forum, cancellationToken);
    }

    private void EstablishSession(Forum forum, CookieContainer cookieContainer)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(forum.BaseUrl)
        };

        // Dispose old client if it exists
        if (_clients.TryRemove(forum.ForumId, out var oldClient))
        {
            oldClient.Dispose();
        }

        var sessionState = new SessionState
        {
            ForumId = forum.ForumId,
            Cookies = cookieContainer,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(_defaultSessionDuration)
        };

        _sessions[forum.ForumId] = sessionState;
        _clients[forum.ForumId] = client;

        _logger.LogInformation(
            "Session established for forum {ForumId} ({ForumName}), expires at {ExpiresAt}",
            forum.ForumId, forum.Name, sessionState.ExpiresAtUtc);
    }

    private async Task<CookieContainer?> TryGetManualCookieAsync(int forumId, string baseUrl, CancellationToken cancellationToken)
    {
        if (_settingRepository is null)
            return null;

        var key = $"{ManualCookieSettingPrefix}{forumId}{ManualCookieSettingSuffix}";
        var cookieValue = await _settingRepository.GetValueAsync(key, cancellationToken);

        if (string.IsNullOrWhiteSpace(cookieValue))
            return null;

        _logger.LogDebug("Found manual session cookie setting for forum {ForumId}", forumId);

        var domain = new Uri(baseUrl).Host;
        var container = new CookieContainer();
        // Parse cookie string: "name=value; name2=value2" format
        foreach (var part in cookieValue.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var name = part[..eqIndex].Trim();
            var value = part[(eqIndex + 1)..].Trim();
            container.Add(new Cookie(name, value, "/", domain));
        }

        return container;
    }

    private async Task DismissAgeVerificationAsync(HttpClient client, Forum forum, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = forum.BaseUrl.TrimEnd('/');
            var ageVerifyUrl = baseUrl + "/?ageVerify=1";
            _logger.LogDebug(
                "Dismissing age verification for forum {ForumId} ({ForumName}): {Url}",
                forum.ForumId, forum.Name, ageVerifyUrl);

            using var response = await client.GetAsync(ageVerifyUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Age verification dismissed for forum {ForumId}", forum.ForumId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Age verification failure is non-fatal — log and continue.
            // The overlay may not be present on all forums or may have already been dismissed.
            _logger.LogWarning(
                ex,
                "Age verification request failed for forum {ForumId} ({ForumName}) — continuing anyway",
                forum.ForumId, forum.Name);
        }
    }

    private void CleanupForumSession(int forumId)
    {
        _sessions.TryRemove(forumId, out _);
        if (_clients.TryRemove(forumId, out var client))
        {
            client.Dispose();
        }
    }
}
