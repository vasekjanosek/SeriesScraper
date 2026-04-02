namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Service contract for resolving forum credentials from environment variables at runtime.
/// The plaintext password is never stored in the database — only the env var key name.
/// </summary>
public interface IForumCredentialService
{
    /// <summary>
    /// Resolves the password for a forum by reading the environment variable
    /// identified by the forum's credential key.
    /// </summary>
    /// <param name="credentialKey">The name of the environment variable holding the password.</param>
    /// <returns>The password value, or null if the environment variable is not set.</returns>
    string? ResolveCredential(string credentialKey);

    /// <summary>
    /// Validates all active forums and returns a list of forums whose credential
    /// environment variables are not set.
    /// </summary>
    /// <returns>List of (forumName, credentialKey) tuples for forums with missing credentials.</returns>
    IReadOnlyList<(string ForumName, string CredentialKey)> ValidateActiveForumCredentials(
        IEnumerable<(string ForumName, string CredentialKey, bool IsActive)> forums);
}
