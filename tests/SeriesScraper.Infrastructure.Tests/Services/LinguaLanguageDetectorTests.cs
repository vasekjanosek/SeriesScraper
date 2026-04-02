using FluentAssertions;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class LinguaLanguageDetectorTests
{
    private readonly LinguaLanguageDetector _sut = new();

    [Fact]
    public void DetectLanguage_EnglishText_ReturnsEn()
    {
        var result = _sut.DetectLanguage(
            "The quick brown fox jumps over the lazy dog and runs through the forest");

        result.Should().Be("en");
    }

    [Fact]
    public void DetectLanguage_CzechText_ReturnsCs()
    {
        var result = _sut.DetectLanguage(
            "Příliš žluťoučký kůň úpěl ďábelské ódy a hrál na xylofon");

        result.Should().Be("cs");
    }

    [Fact]
    public void DetectLanguage_GermanText_ReturnsDe()
    {
        var result = _sut.DetectLanguage(
            "Der schnelle braune Fuchs springt über den faulen Hund und läuft durch den Wald");

        result.Should().Be("de");
    }

    [Fact]
    public void DetectLanguage_FrenchText_ReturnsFr()
    {
        var result = _sut.DetectLanguage(
            "Le renard brun rapide saute par-dessus le chien paresseux dans la forêt");

        result.Should().Be("fr");
    }

    [Fact]
    public void DetectLanguage_NullText_ReturnsNull()
    {
        var result = _sut.DetectLanguage(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void DetectLanguage_EmptyText_ReturnsNull()
    {
        var result = _sut.DetectLanguage("");

        result.Should().BeNull();
    }

    [Fact]
    public void DetectLanguage_WhitespaceOnly_ReturnsNull()
    {
        var result = _sut.DetectLanguage("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void DetectLanguage_ReturnsIso6391LowerCase()
    {
        var result = _sut.DetectLanguage(
            "This is a simple English sentence about movies and television series");

        result.Should().NotBeNull();
        result.Should().MatchRegex("^[a-z]{2}$");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("!@#$%^&*()")]
    [InlineData("+++---===")]
    [InlineData(".,;:!?")]
    public void DetectLanguage_NonAlphabeticInput_ReturnsNull(string input)
    {
        var result = _sut.DetectLanguage(input);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("x")]
    public void DetectLanguage_VeryShortText_ReturnsNullOrValid(string input)
    {
        var result = _sut.DetectLanguage(input);

        // Very short text should either return null (Unknown) or a valid 2-char ISO code
        if (result != null)
            result.Should().MatchRegex("^[a-z]{2}$");
    }

    [Fact]
    public void DetectLanguage_TabsAndNewlines_ReturnsNull()
    {
        var result = _sut.DetectLanguage("\t\n\r");

        result.Should().BeNull();
    }

    [Fact]
    public void DetectLanguage_WhenDetectionCoreThrows_ReturnsNull()
    {
        var throwing = new ThrowingLanguageDetector();

        var result = throwing.DetectLanguage("This should not throw outward");

        result.Should().BeNull();
    }

    private class ThrowingLanguageDetector : LinguaLanguageDetector
    {
        protected override string? DetectLanguageCore(string text)
        {
            throw new InvalidOperationException("Lingua internal error");
        }
    }
}
