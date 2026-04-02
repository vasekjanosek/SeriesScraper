namespace SeriesScraper.Domain.Interfaces;

public interface ILanguageDetector
{
    string? DetectLanguage(string text);
}
