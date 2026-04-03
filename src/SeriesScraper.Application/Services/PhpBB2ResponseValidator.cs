using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Detects phpBB2 session expiry by checking if the response HTML
/// contains login form indicators instead of the expected content.
/// </summary>
public sealed class PhpBB2ResponseValidator : IResponseValidator
{
    /// <summary>
    /// Login form patterns that indicate phpBB2 has returned a login page
    /// instead of the requested content (i.e., session has expired).
    /// </summary>
    private static readonly string[] LoginFormIndicators =
    [
        "action=\"login.php\"",
        "action='login.php'",
        "action=\"./login.php\"",
        "action='./login.php'",
        "name=\"login\"",
        "name='login'",
        "id=\"login\"",
        "id='login'",
    ];

    /// <inheritdoc />
    public bool IsSessionExpired(string responseHtml)
    {
        if (string.IsNullOrWhiteSpace(responseHtml))
            return false;

        // Check for phpBB2 login form indicators (case-insensitive)
        foreach (var indicator in LoginFormIndicators)
        {
            if (responseHtml.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                // Additional heuristic: ensure this is actually a login form page,
                // not just a page that happens to have a login link in a sidebar.
                // phpBB2 login pages have a <form> element with the indicator.
                if (responseHtml.Contains("<form", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
