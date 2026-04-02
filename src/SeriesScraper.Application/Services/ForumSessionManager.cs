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
/// </summary>
public sealed class ForumSessionManager : IForumSessionManager
{
    private readonly IForumScraper _forumScraper;
    private readonly IForumCredentialService _credentialService;
    private readonly ILogger<ForumSessionManager> _logger;

    private readonly ConcurrentDictionary<int, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<int, HttpClient> _clients = new();

    private readonly TimeSpan _defaultSessionDuration;
    private bool _disposed;

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
    public ForumSessionManager(
        IForumScraper forumScraper,
        IForumCredentialService credentialService,
        ILogger<ForumSessionManager> logger,
        TimeSpan? defaultSessionDuration = null)
    {
        _forumScraper = forumScraper ?? throw new ArgumentNullException(nameof(forumScraper));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultSessionDuration = defaultSessionDuration ?? TimeSpan.FromMinutes(30);
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

        _logger.LogInformation(
            "Authenticating to forum {ForumId} ({ForumName}) as {Username}",
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

        // Use the scraper's CookieContainer so auth cookies are shared
        var cookieContainer = _forumScraper.GetCookieContainer();
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

    private void CleanupForumSession(int forumId)
    {
        _sessions.TryRemove(forumId, out _);
        if (_clients.TryRemove(forumId, out var client))
        {
            client.Dispose();
        }
    }
}
