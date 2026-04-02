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
}
