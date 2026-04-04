using FluentAssertions;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class LanguageTagParserTests
{
    private readonly LanguageTagParser _sut = new();

    // ── ParseLanguageTags ────────────────────────────────────

    [Fact]
    public void ParseLanguageTags_NullInput_ReturnsEmpty()
    {
        _sut.ParseLanguageTags(null!).Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_EmptyInput_ReturnsEmpty()
    {
        _sut.ParseLanguageTags("").Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_WhitespaceInput_ReturnsEmpty()
    {
        _sut.ParseLanguageTags("   ").Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_NoSlash_ReturnsEmpty()
    {
        _sut.ParseLanguageTags("Some Movie Title").Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_SingleSlashWithLanguageCodes_ReturnsMapped()
    {
        var result = _sut.ParseLanguageTags("Tenkrát poprvé / CZ, EN");

        result.Should().HaveCount(2);
        result.Should().ContainInOrder("cs", "en");
    }

    [Fact]
    public void ParseLanguageTags_MultipleSlashesWithLanguageCodes_ParsesLastSegment()
    {
        var result = _sut.ParseLanguageTags("Tenkrát poprvé / Never Have I Ever / CZ, EN");

        result.Should().HaveCount(2);
        result.Should().ContainInOrder("cs", "en");
    }

    [Fact]
    public void ParseLanguageTags_SingleLanguageCode_ReturnsSingle()
    {
        var result = _sut.ParseLanguageTags("Čmuchalovci 2 / Bloodhounds /CZ");

        result.Should().HaveCount(1);
        result[0].Should().Be("cs");
    }

    [Fact]
    public void ParseLanguageTags_InconsistentWhitespace_StillParses()
    {
        var result = _sut.ParseLanguageTags("Title /CZ,EN");

        result.Should().HaveCount(2);
        result.Should().ContainInOrder("cs", "en");
    }

    [Fact]
    public void ParseLanguageTags_SpacesAroundCodes_TrimsProperly()
    {
        var result = _sut.ParseLanguageTags("Title / CZ , EN , SK ");

        result.Should().HaveCount(3);
        result.Should().ContainInOrder("cs", "en", "sk");
    }

    [Fact]
    public void ParseLanguageTags_SlovakCode_MapsToSk()
    {
        var result = _sut.ParseLanguageTags("Title / SK");

        result.Should().ContainSingle().Which.Should().Be("sk");
    }

    [Fact]
    public void ParseLanguageTags_GermanCode_MapsToDe()
    {
        var result = _sut.ParseLanguageTags("Title / DE");

        result.Should().ContainSingle().Which.Should().Be("de");
    }

    [Fact]
    public void ParseLanguageTags_MixedCase_HandlesCorrectly()
    {
        var result = _sut.ParseLanguageTags("Title / cz, En, sk");

        result.Should().HaveCount(3);
        result.Should().ContainInOrder("cs", "en", "sk");
    }

    [Fact]
    public void ParseLanguageTags_UnknownCode_ReturnsLowercased()
    {
        var result = _sut.ParseLanguageTags("Title / XX");

        result.Should().ContainSingle().Which.Should().Be("xx");
    }

    [Fact]
    public void ParseLanguageTags_LastSegmentNotCodes_ReturnsEmpty()
    {
        // If the last segment contains non-code text, treat it as not a language segment
        var result = _sut.ParseLanguageTags("Title / Another Title / This is not a code");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_LastSegmentEmptyAfterSlash_ReturnsEmpty()
    {
        var result = _sut.ParseLanguageTags("Title / ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseLanguageTags_DuplicateCodes_Deduplicates()
    {
        var result = _sut.ParseLanguageTags("Title / CZ, CZ, EN");

        result.Should().HaveCount(2);
        result.Should().ContainInOrder("cs", "en");
    }

    [Fact]
    public void ParseLanguageTags_UkraineCode_MapsToUk()
    {
        var result = _sut.ParseLanguageTags("Title / UA");

        result.Should().ContainSingle().Which.Should().Be("uk");
    }

    [Fact]
    public void ParseLanguageTags_JapaneseCode_MapsToJa()
    {
        var result = _sut.ParseLanguageTags("Title / JP");

        result.Should().ContainSingle().Which.Should().Be("ja");
    }

    [Fact]
    public void ParseLanguageTags_ThreeLetterCode_Accepted()
    {
        // Three-letter codes are valid per the regex
        var result = _sut.ParseLanguageTags("Title / CZE");

        result.Should().ContainSingle().Which.Should().Be("cze");
    }

    [Fact]
    public void ParseLanguageTags_CodeWithNumbers_ReturnsEmpty()
    {
        // Codes with numbers are not valid country codes
        var result = _sut.ParseLanguageTags("Title / C1, 2E");

        result.Should().BeEmpty();
    }

    // ── GetLanguageString ────────────────────────────────────

    [Fact]
    public void GetLanguageString_NullInput_ReturnsNull()
    {
        _sut.GetLanguageString(null!).Should().BeNull();
    }

    [Fact]
    public void GetLanguageString_NoTags_ReturnsNull()
    {
        _sut.GetLanguageString("Simple Title").Should().BeNull();
    }

    [Fact]
    public void GetLanguageString_SingleCode_ReturnsIsoString()
    {
        _sut.GetLanguageString("Title / CZ").Should().Be("cs");
    }

    [Fact]
    public void GetLanguageString_MultipleCodes_ReturnsCommaSeparated()
    {
        _sut.GetLanguageString("Title / CZ, EN").Should().Be("cs,en");
    }

    [Fact]
    public void GetLanguageString_ThreeCodes_ReturnsCommaSeparated()
    {
        _sut.GetLanguageString("Title / CZ, EN, SK").Should().Be("cs,en,sk");
    }

    [Fact]
    public void GetLanguageString_FullForumTitle_ParsesCorrectly()
    {
        _sut.GetLanguageString("Tenkrát poprvé / Never Have I Ever / CZ, EN").Should().Be("cs,en");
    }

    [Fact]
    public void GetLanguageString_BloodhoundsExample_ParsesCorrectly()
    {
        _sut.GetLanguageString("Čmuchalovci 2 / Bloodhounds /CZ").Should().Be("cs");
    }

    // ── All mapped codes ────────────────────────────────────

    [Theory]
    [InlineData("CZ", "cs")]
    [InlineData("SK", "sk")]
    [InlineData("EN", "en")]
    [InlineData("DE", "de")]
    [InlineData("FR", "fr")]
    [InlineData("ES", "es")]
    [InlineData("IT", "it")]
    [InlineData("PL", "pl")]
    [InlineData("HU", "hu")]
    [InlineData("RU", "ru")]
    [InlineData("PT", "pt")]
    [InlineData("NL", "nl")]
    [InlineData("RO", "ro")]
    [InlineData("HR", "hr")]
    [InlineData("SR", "sr")]
    [InlineData("BG", "bg")]
    [InlineData("UA", "uk")]
    [InlineData("JP", "ja")]
    [InlineData("KR", "ko")]
    [InlineData("CN", "zh")]
    public void ParseLanguageTags_AllKnownMappings_MapCorrectly(string input, string expected)
    {
        var result = _sut.ParseLanguageTags($"Title / {input}");

        result.Should().ContainSingle().Which.Should().Be(expected);
    }
}
