namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Value object representing forum authentication credentials.
/// </summary>
public sealed record ForumCredentials
{
    /// <summary>
    /// The forum username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The forum password.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// The forum base URL (e.g., "https://forum.example.com").
    /// Used by IForumScraper implementations to know where to authenticate.
    /// </summary>
    public string? BaseUrl { get; init; }
}
