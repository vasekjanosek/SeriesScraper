using System.Text.RegularExpressions;

namespace SeriesScraper.Application.Security;

/// <summary>
/// Provides safe regex operations with enforced timeouts to prevent ReDoS attacks.
/// All regex operations in the application should use this helper instead of creating
/// Regex instances directly.
/// </summary>
public static class SafeRegex
{
    /// <summary>
    /// Default timeout for all regex operations (2 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Creates a new Regex with enforced timeout. If the caller specifies a timeout,
    /// it will be capped at DefaultTimeout.
    /// </summary>
    public static Regex Create(string pattern, RegexOptions options = RegexOptions.None, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout.HasValue && timeout.Value < DefaultTimeout
            ? timeout.Value
            : DefaultTimeout;

        return new Regex(pattern, options, effectiveTimeout);
    }

    /// <summary>
    /// Attempts to match the input against the pattern with a timeout.
    /// Returns null on timeout instead of throwing.
    /// </summary>
    public static Match? SafeMatch(string input, string pattern, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var regex = new Regex(pattern, options, DefaultTimeout);
            var match = regex.Match(input);
            return match.Success ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to check if input matches the pattern with a timeout.
    /// Returns false on timeout instead of throwing.
    /// </summary>
    public static bool SafeIsMatch(string input, string pattern, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var regex = new Regex(pattern, options, DefaultTimeout);
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the global default regex match timeout for the entire application.
    /// This serves as a safety net for any Regex instances created without explicit timeouts.
    /// Should be called once at application startup.
    /// </summary>
    public static void SetGlobalTimeout()
    {
        AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", DefaultTimeout);
    }
}
