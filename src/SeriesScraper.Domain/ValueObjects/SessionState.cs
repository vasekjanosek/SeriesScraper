using System.Net;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Value object representing the state of an authenticated forum session.
/// </summary>
public sealed record SessionState
{
    /// <summary>
    /// The ID of the forum this session belongs to.
    /// </summary>
    public required int ForumId { get; init; }

    /// <summary>
    /// The cookie container holding session cookies.
    /// </summary>
    public required CookieContainer Cookies { get; init; }

    /// <summary>
    /// When this session was created (UTC).
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When this session expires (UTC). Null means no known expiry.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>
    /// Whether the session has expired based on the expiry timestamp.
    /// Sessions with no expiry are never considered expired by time alone.
    /// </summary>
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTime.UtcNow >= ExpiresAtUtc.Value;
}
