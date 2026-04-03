using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Represents a configured forum to be scraped.
/// Credentials are stored as environment variable keys, not plaintext passwords.
/// </summary>
public class Forum
{
    private string _credentialKey = null!;

    public int ForumId { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public required string Username { get; set; }
    
    /// <summary>
    /// Name of the environment variable containing the forum password.
    /// NEVER contains the actual password (security requirement).
    /// Validated to match the FORUM_* pattern to prevent arbitrary env var reads (#51).
    /// </summary>
    public required string CredentialKey
    {
        get => _credentialKey;
        set
        {
            _ = new CredentialKey(value);
            _credentialKey = value;
        }
    }
    
    /// <summary>
    /// Encrypted password for forum authentication, protected via DataProtection API.
    /// Null when no password has been stored yet.
    /// </summary>
    public string? EncryptedPassword { get; set; }
    
    public int CrawlDepth { get; set; } = 1;
    public int PolitenessDelayMs { get; set; } = 500;
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<ForumSection> Sections { get; set; } = new List<ForumSection>();
    public ICollection<ScrapeRun> ScrapeRuns { get; set; } = new List<ScrapeRun>();
}
