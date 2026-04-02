using System.Text.RegularExpressions;

namespace SeriesScraper.Domain.ValueObjects;

/// <summary>
/// Value object that represents a validated environment variable key for forum credentials.
/// Enforces the naming convention: must match ^FORUM_[A-Z0-9_]+$ to prevent
/// arbitrary environment variable reads (security requirement from #51).
/// </summary>
public sealed partial record CredentialKey
{
    /// <summary>
    /// The regex pattern that credential keys must match.
    /// Only allows keys starting with FORUM_ followed by uppercase letters, digits, or underscores.
    /// </summary>
    public const string Pattern = @"^FORUM_[A-Z0-9_]+$";

    /// <summary>
    /// The validated credential key value.
    /// </summary>
    public string Value { get; }

    public CredentialKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Credential key cannot be null or empty.", nameof(value));

        if (!ValidationRegex().IsMatch(value))
            throw new ArgumentException(
                $"Credential key '{value}' does not match the required pattern '{Pattern}'. " +
                "Keys must start with 'FORUM_' followed by uppercase letters, digits, or underscores.",
                nameof(value));

        Value = value;
    }

    [GeneratedRegex(Pattern)]
    private static partial Regex ValidationRegex();

    public override string ToString() => Value;

    public static implicit operator string(CredentialKey key) => key.Value;
}
