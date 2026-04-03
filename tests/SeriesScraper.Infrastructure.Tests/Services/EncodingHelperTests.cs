using System.Net;
using System.Text;
using FluentAssertions;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class EncodingHelperTests
{
    static EncodingHelperTests()
    {
        // Ensure code pages are registered for all tests
        EncodingHelper.RegisterCodePages();
    }

    // ── ResolveEncoding ────────────────────────────────────────────────

    [Fact]
    public void ResolveEncoding_Utf8_ReturnsUtf8()
    {
        var encoding = EncodingHelper.ResolveEncoding("utf-8");
        encoding.WebName.Should().Be("utf-8");
    }

    [Fact]
    public void ResolveEncoding_Windows1250_ReturnsWindows1250()
    {
        var encoding = EncodingHelper.ResolveEncoding("windows-1250");
        encoding.CodePage.Should().Be(1250);
    }

    [Fact]
    public void ResolveEncoding_Null_ReturnsUtf8()
    {
        var encoding = EncodingHelper.ResolveEncoding(null);
        encoding.WebName.Should().Be("utf-8");
    }

    [Fact]
    public void ResolveEncoding_Empty_ReturnsUtf8()
    {
        var encoding = EncodingHelper.ResolveEncoding("");
        encoding.WebName.Should().Be("utf-8");
    }

    [Fact]
    public void ResolveEncoding_Unknown_ReturnsUtf8()
    {
        var encoding = EncodingHelper.ResolveEncoding("totally-made-up-encoding");
        encoding.WebName.Should().Be("utf-8");
    }

    // ── ReadResponseAsStringAsync ──────────────────────────────────────

    [Fact]
    public async Task ReadResponseAsStringAsync_Utf8Content_ReadsCorrectly()
    {
        var text = "Hello World — příliš žluťoučký kůň";
        var response = CreateResponseFromString(text, Encoding.UTF8);

        var result = await EncodingHelper.ReadResponseAsStringAsync(response, "utf-8");

        result.Should().Be(text);
    }

    [Fact]
    public async Task ReadResponseAsStringAsync_Windows1250Content_ReadsCorrectly()
    {
        // Czech text with diacritics
        var text = "ěščřžýáíéúůďťňĚŠČŘŽÝÁÍÉÚŮĎŤŇ";
        var win1250 = Encoding.GetEncoding(1250);
        var response = CreateResponseFromString(text, win1250);

        var result = await EncodingHelper.ReadResponseAsStringAsync(response, "windows-1250");

        result.Should().Be(text);
    }

    [Fact]
    public async Task ReadResponseAsStringAsync_Windows1250Content_WithWrongEncoding_CorruptsText()
    {
        // Demonstrate that using wrong encoding produces garbled text
        var text = "ěščřžýáíé";
        var win1250 = Encoding.GetEncoding(1250);
        var response = CreateResponseFromString(text, win1250);

        var result = await EncodingHelper.ReadResponseAsStringAsync(response, "utf-8");

        // The text should NOT match when read with wrong encoding
        result.Should().NotBe(text);
    }

    [Fact]
    public async Task ReadResponseAsStringAsync_NullEncoding_DefaultsToUtf8()
    {
        var text = "Hello World";
        var response = CreateResponseFromString(text, Encoding.UTF8);

        var result = await EncodingHelper.ReadResponseAsStringAsync(response, null);

        result.Should().Be(text);
    }

    [Fact]
    public async Task ReadResponseAsStringAsync_NullResponse_ThrowsArgumentNullException()
    {
        var act = () => EncodingHelper.ReadResponseAsStringAsync(null!, "utf-8");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Czech character round-trip ─────────────────────────────────────

    [Fact]
    public async Task CzechCharacters_RoundTrip_Windows1250_PreservesAllDiacritics()
    {
        var czechText = "Příliš žluťoučký kůň úpěl ďábelské ódy";
        var win1250 = Encoding.GetEncoding(1250);

        // Encode to bytes as Windows-1250 (simulating server response)
        var response = CreateResponseFromString(czechText, win1250);

        // Decode back using EncodingHelper
        var decoded = await EncodingHelper.ReadResponseAsStringAsync(response, "windows-1250");

        decoded.Should().Be(czechText);
        decoded.Should().Contain("ě");
        decoded.Should().Contain("š");
        decoded.Should().Contain("č");
        decoded.Should().Contain("ř");
        decoded.Should().Contain("ž");
        decoded.Should().Contain("ý");
        decoded.Should().Contain("á");
        decoded.Should().Contain("í");
        decoded.Should().Contain("é");
        decoded.Should().Contain("ú");
        decoded.Should().Contain("ů");
        decoded.Should().Contain("ď");
        decoded.Should().Contain("ť"); // included in žluťoučký
        decoded.Should().Contain("ň"); // included in kůň
    }

    // ── RegisterCodePages ──────────────────────────────────────────────

    [Fact]
    public void RegisterCodePages_AllowsWindows1250()
    {
        // This should not throw after RegisterCodePages
        var encoding = Encoding.GetEncoding(1250);
        encoding.Should().NotBeNull();
        encoding.CodePage.Should().Be(1250);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static HttpResponseMessage CreateResponseFromString(string text, Encoding encoding)
    {
        var bytes = encoding.GetBytes(text);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
        return response;
    }
}
