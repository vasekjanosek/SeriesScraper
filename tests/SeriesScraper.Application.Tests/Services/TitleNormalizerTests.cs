using FluentAssertions;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class TitleNormalizerTests
{
    private readonly TitleNormalizer _sut = new();

    // ── Normalize ──────────────────────────────────────────────────

    [Fact]
    public void Normalize_NullInput_ReturnsEmpty()
    {
        _sut.Normalize(null!).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        _sut.Normalize("").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_WhitespaceInput_ReturnsEmpty()
    {
        _sut.Normalize("   ").Should().BeEmpty();
    }

    [Fact]
    public void Normalize_SimpleTitle_Lowercases()
    {
        _sut.Normalize("Breaking Bad").Should().Be("breaking bad");
    }

    [Fact]
    public void Normalize_TitleWithPunctuation_RemovesPunctuation()
    {
        _sut.Normalize("Spider-Man: No Way Home").Should().Be("spiderman no way home");
    }

    [Fact]
    public void Normalize_TitleWithDiacritics_StripsDiacritics()
    {
        _sut.Normalize("Amélie").Should().Be("amelie");
    }

    [Fact]
    public void Normalize_CzechDiacritics_StripsDiacritics()
    {
        _sut.Normalize("Příběhy ze šťastného kraje").Should().Be("pribehy ze stastneho kraje");
    }

    [Fact]
    public void Normalize_TitleWithYear_StripsYear()
    {
        _sut.Normalize("The Matrix (1999)").Should().Be("the matrix");
    }

    [Fact]
    public void Normalize_TitleWithExtraWhitespace_CollapsesWhitespace()
    {
        _sut.Normalize("  The   Matrix   ").Should().Be("the matrix");
    }

    [Fact]
    public void Normalize_TitleWithNumbers_PreservesNumbers()
    {
        _sut.Normalize("2001 A Space Odyssey").Should().Be("2001 a space odyssey");
    }

    [Fact]
    public void Normalize_MixedCase_NormalizesConsistently()
    {
        _sut.Normalize("ThE dArK kNiGhT").Should().Be("the dark knight");
    }

    [Fact]
    public void Normalize_TitleWithApostrophe_RemovesApostrophe()
    {
        _sut.Normalize("Schindler's List").Should().Be("schindlers list");
    }

    // ── ExtractYear ────────────────────────────────────────────────

    [Fact]
    public void ExtractYear_NullInput_ReturnsNull()
    {
        _sut.ExtractYear(null!).Should().BeNull();
    }

    [Fact]
    public void ExtractYear_EmptyInput_ReturnsNull()
    {
        _sut.ExtractYear("").Should().BeNull();
    }

    [Fact]
    public void ExtractYear_NoYear_ReturnsNull()
    {
        _sut.ExtractYear("Breaking Bad").Should().BeNull();
    }

    [Fact]
    public void ExtractYear_YearInParentheses_ExtractsYear()
    {
        _sut.ExtractYear("The Matrix (1999)").Should().Be(1999);
    }

    [Fact]
    public void ExtractYear_YearNotInParentheses_ReturnsNull()
    {
        _sut.ExtractYear("2001 A Space Odyssey").Should().BeNull();
    }

    [Fact]
    public void ExtractYear_MultipleYears_ExtractsFirst()
    {
        _sut.ExtractYear("Movie (2020) (2021)").Should().Be(2020);
    }

    [Fact]
    public void ExtractYear_RecentYear_Extracts()
    {
        _sut.ExtractYear("New Movie (2025)").Should().Be(2025);
    }

    // ── StripYear ──────────────────────────────────────────────────

    [Fact]
    public void StripYear_NullInput_ReturnsEmpty()
    {
        _sut.StripYear(null!).Should().BeEmpty();
    }

    [Fact]
    public void StripYear_EmptyInput_ReturnsEmpty()
    {
        _sut.StripYear("").Should().BeEmpty();
    }

    [Fact]
    public void StripYear_NoYear_ReturnsSameTrimmed()
    {
        _sut.StripYear("Breaking Bad").Should().Be("Breaking Bad");
    }

    [Fact]
    public void StripYear_YearSuffix_RemovesYear()
    {
        _sut.StripYear("The Matrix (1999)").Should().Be("The Matrix");
    }

    [Fact]
    public void StripYear_YearMidString_RemovesYear()
    {
        _sut.StripYear("Movie (2020) Extended").Should().Be("Movie Extended");
    }

    // ── ComputeSimilarity ──────────────────────────────────────────

    [Fact]
    public void ComputeSimilarity_BothEmpty_Returns1()
    {
        _sut.ComputeSimilarity("", "").Should().Be(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_OneEmpty_Returns0()
    {
        _sut.ComputeSimilarity("test", "").Should().Be(0.0m);
    }

    [Fact]
    public void ComputeSimilarity_OtherEmpty_Returns0()
    {
        _sut.ComputeSimilarity("", "test").Should().Be(0.0m);
    }

    [Fact]
    public void ComputeSimilarity_BothNull_Returns1()
    {
        _sut.ComputeSimilarity(null!, null!).Should().Be(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_OneNull_Returns0()
    {
        _sut.ComputeSimilarity("test", null!).Should().Be(0.0m);
    }

    [Fact]
    public void ComputeSimilarity_IdenticalStrings_Returns1()
    {
        _sut.ComputeSimilarity("Breaking Bad", "Breaking Bad").Should().Be(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_CaseInsensitive_Returns1()
    {
        _sut.ComputeSimilarity("breaking bad", "BREAKING BAD").Should().Be(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_DiacriticsEquivalent_Returns1()
    {
        _sut.ComputeSimilarity("Amélie", "Amelie").Should().Be(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_SimilarStrings_ReturnsHighScore()
    {
        var score = _sut.ComputeSimilarity("Breaking Bad", "Breaking Bed");
        score.Should().BeGreaterThan(0.8m).And.BeLessThan(1.0m);
    }

    [Fact]
    public void ComputeSimilarity_DifferentStrings_ReturnsLowScore()
    {
        var score = _sut.ComputeSimilarity("Breaking Bad", "Game of Thrones");
        score.Should().BeLessThan(0.5m);
    }

    [Fact]
    public void ComputeSimilarity_WithYearsInBoth_NormalizesOut()
    {
        var score = _sut.ComputeSimilarity("The Matrix (1999)", "The Matrix (2003)");
        score.Should().Be(1.0m);
    }

    // ── Internal: RemoveDiacritics ─────────────────────────────────

    [Fact]
    public void RemoveDiacritics_PlainAscii_Unchanged()
    {
        TitleNormalizer.RemoveDiacritics("Hello World").Should().Be("Hello World");
    }

    [Fact]
    public void RemoveDiacritics_AccentedCharacters_Stripped()
    {
        TitleNormalizer.RemoveDiacritics("é à ü ö ñ").Should().Be("e a u o n");
    }

    [Fact]
    public void RemoveDiacritics_CzechCharacters_Stripped()
    {
        TitleNormalizer.RemoveDiacritics("čřžšďťňěů").Should().Be("crzsdtneu");
    }

    // ── Internal: LevenshteinDistance ───────────────────────────────

    [Fact]
    public void LevenshteinDistance_IdenticalStrings_ReturnsZero()
    {
        TitleNormalizer.LevenshteinDistance("abc", "abc").Should().Be(0);
    }

    [Fact]
    public void LevenshteinDistance_OneEmpty_ReturnsOtherLength()
    {
        TitleNormalizer.LevenshteinDistance("", "abc").Should().Be(3);
    }

    [Fact]
    public void LevenshteinDistance_OtherEmpty_ReturnsFirstLength()
    {
        TitleNormalizer.LevenshteinDistance("abc", "").Should().Be(3);
    }

    [Fact]
    public void LevenshteinDistance_OneCharDifference_ReturnsOne()
    {
        TitleNormalizer.LevenshteinDistance("abc", "adc").Should().Be(1);
    }

    [Fact]
    public void LevenshteinDistance_Insertion_ReturnsOne()
    {
        TitleNormalizer.LevenshteinDistance("abc", "abcd").Should().Be(1);
    }

    [Fact]
    public void LevenshteinDistance_Deletion_ReturnsOne()
    {
        TitleNormalizer.LevenshteinDistance("abcd", "abc").Should().Be(1);
    }

    [Fact]
    public void LevenshteinDistance_CompletelyDifferent_ReturnsMaxLength()
    {
        TitleNormalizer.LevenshteinDistance("abc", "xyz").Should().Be(3);
    }
}
