namespace SeriesScraper.Domain.Exceptions;

/// <summary>
/// Exception thrown when a scraping operation fails due to network, protocol, or parsing errors.
/// </summary>
public class ScrapingException : Exception
{
    public ScrapingException()
    {
    }

    public ScrapingException(string message)
        : base(message)
    {
    }

    public ScrapingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
