using Microsoft.Playwright;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Default implementation that creates an IPlaywright instance via the Playwright static factory.
/// </summary>
public sealed class PlaywrightFactory : IPlaywrightFactory
{
    public Task<IPlaywright> CreateAsync() => Playwright.CreateAsync();
}
