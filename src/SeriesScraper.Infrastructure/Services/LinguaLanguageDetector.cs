using Lingua;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Services;

public class LinguaLanguageDetector : ILanguageDetector
{
    private readonly LanguageDetector _detector;

    public LinguaLanguageDetector()
    {
        _detector = LanguageDetectorBuilder
            .FromAllLanguages()
            .Build();
    }

    public string? DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return DetectLanguageCore(text);
        }
        catch
        {
            return null;
        }
    }

    protected virtual string? DetectLanguageCore(string text)
    {
        var language = _detector.DetectLanguageOf(text);

        if (language == Language.Unknown)
            return null;

        var isoCode = language.IsoCode6391();

        if (isoCode == IsoCode6391.None)
            return null;

        return isoCode.ToString().ToLowerInvariant();
    }
}
