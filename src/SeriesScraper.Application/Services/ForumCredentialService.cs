using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Resolves forum credentials from environment variables at runtime.
/// Validates credential key format before reading environment variables.
/// </summary>
public class ForumCredentialService : IForumCredentialService
{
    /// <inheritdoc />
    public string? ResolveCredential(string credentialKey)
    {
        // Validate the key format before reading the environment variable.
        // This prevents arbitrary env var reads (security requirement #51).
        var key = new CredentialKey(credentialKey);
        return Environment.GetEnvironmentVariable(key.Value);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string ForumName, string CredentialKey)> ValidateActiveForumCredentials(
        IEnumerable<(string ForumName, string CredentialKey, bool IsActive)> forums)
    {
        var missing = new List<(string ForumName, string CredentialKey)>();

        foreach (var (forumName, credentialKey, isActive) in forums)
        {
            if (!isActive)
                continue;

            // Validate key format before reading env var (security requirement #51)
            var key = new CredentialKey(credentialKey);
            var envValue = Environment.GetEnvironmentVariable(key.Value);
            if (string.IsNullOrEmpty(envValue))
            {
                missing.Add((forumName, credentialKey));
            }
        }

        return missing.AsReadOnly();
    }
}
