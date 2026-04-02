using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Interfaces;

public interface IHtmlForumSectionParser
{
    IReadOnlyList<ForumSection> ParseSections(string html, string baseUrl);
}
