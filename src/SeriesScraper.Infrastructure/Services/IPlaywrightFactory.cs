using Microsoft.Playwright;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Abstracts Playwright instance creation to enable unit testing of
/// classes that depend on IPlaywright.
/// </summary>
public interface IPlaywrightFactory
{
    Task<IPlaywright> CreateAsync();
}
