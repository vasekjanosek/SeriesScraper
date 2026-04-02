using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class LinkExtractorServiceTests
{
    private readonly Mock<ILinkTypeService> _linkTypeServiceMock = new();
    private readonly Mock<ILogger<LinkExtractorService>> _loggerMock = new();
    private readonly LinkExtractorService _sut;

    private static readonly List<LinkType> DefaultLinkTypes = new()
    {
        new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsSystem = true, IsActive = true },
        new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsSystem = true, IsActive = true },
        new() { LinkTypeId = 3, Name = "Magnet URI", UrlPattern = @"^magnet:\?", IsSystem = true, IsActive = true },
        new() { LinkTypeId = 4, Name = "Cloud Storage URL", UrlPattern = @"(drive\.google|dropbox|mega\.nz)", IsSystem = true, IsActive = true }
    };

    public LinkExtractorServiceTests()
    {
        _linkTypeServiceMock
            .Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLinkTypes);

        // Default: classify HTTP URLs as type 1
        _linkTypeServiceMock
            .Setup(s => s.ClassifyUrl(It.Is<string>(u => u.StartsWith("http")), It.IsAny<IReadOnlyList<LinkType>>()))
            .Returns(1);

        // Magnet URIs as type 3
        _linkTypeServiceMock
            .Setup(s => s.ClassifyUrl(It.Is<string>(u => u.StartsWith("magnet:")), It.IsAny<IReadOnlyList<LinkType>>()))
            .Returns(3);

        _sut = new LinkExtractorService(_linkTypeServiceMock.Object, _loggerMock.Object);
    }

    // ── ExtractLinksAsync ────────────────────────────────────

    [Fact]
    public async Task ExtractLinksAsync_EmptyHtml_ReturnsEmpty()
    {
        var result = await _sut.ExtractLinksAsync("", 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_NullHtml_ReturnsEmpty()
    {
        var result = await _sut.ExtractLinksAsync(null!, 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_WhitespaceHtml_ReturnsEmpty()
    {
        var result = await _sut.ExtractLinksAsync("   ", 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_HtmlWithHttpLink_ExtractsLink()
    {
        var html = """<a href="https://example.com/file.zip">Download</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://example.com/file.zip");
        result[0].LinkTypeId.Should().Be(1);
        result[0].RunId.Should().Be(1);
        result[0].PostUrl.Should().Be("https://forum.example.com/post/1");
        result[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractLinksAsync_MultipleLinks_ExtractsAll()
    {
        var html = """
            <a href="https://example.com/file1.zip">File 1</a>
            <a href="https://example.com/file2.zip">File 2</a>
            """;

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExtractLinksAsync_DuplicateUrls_DeduplicatesBeforeCreating()
    {
        var html = """
            <a href="https://example.com/file.zip">Click</a>
            <a href="https://example.com/file.zip">Click again</a>
            """;

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtractLinksAsync_MagnetUri_ExtractsLink()
    {
        var html = """<a href="magnet:?xt=urn:btih:abc123">Magnet</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
        result[0].Url.Should().StartWith("magnet:");
        result[0].LinkTypeId.Should().Be(3);
    }

    [Fact]
    public async Task ExtractLinksAsync_JavascriptUrl_Stripped()
    {
        var html = """<a href="javascript:alert(1)">XSS</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_DataUrl_Stripped()
    {
        var html = """<a href="data:text/html,<h1>Evil</h1>">Data</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_UrlWithSeasonEpisode_ParsesCorrectly()
    {
        var html = """<a href="https://example.com/show-S02E05-720p.zip">Episode</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
        result[0].ParsedSeason.Should().Be(2);
        result[0].ParsedEpisode.Should().Be(5);
    }

    [Fact]
    public async Task ExtractLinksAsync_UrlWithoutSeasonEpisode_NullValues()
    {
        var html = """<a href="https://dl.host.com/film-2024.zip">Movie</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
        result[0].ParsedSeason.Should().BeNull();
        result[0].ParsedEpisode.Should().BeNull();
    }

    [Fact]
    public async Task ExtractLinksAsync_UnclassifiedUrl_SkipsLink()
    {
        _linkTypeServiceMock
            .Setup(s => s.ClassifyUrl(It.IsAny<string>(), It.IsAny<IReadOnlyList<LinkType>>()))
            .Returns((int?)null);

        var html = """<a href="https://unmatched.example.com/file">Test</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_PlainTextUrls_ExtractsFromText()
    {
        var html = "Download here: https://example.com/file.zip and enjoy";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://example.com/file.zip");
    }

    [Fact]
    public async Task ExtractLinksAsync_FtpScheme_Stripped()
    {
        var html = """<a href="ftp://example.com/file.zip">FTP</a>""";

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_SetsCreatedAtToUtcNow()
    {
        var html = """<a href="https://example.com/file.zip">Download</a>""";
        var before = DateTime.UtcNow;

        var result = await _sut.ExtractLinksAsync(html, 1, "https://forum.example.com/post/1");

        var after = DateTime.UtcNow;
        result[0].CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── ExtractUrls (static) ─────────────────────────────────

    [Fact]
    public void ExtractUrls_HrefAndPlainText_ExtractsBoth()
    {
        var html = """<a href="https://a.com/1">Link</a> visit https://b.com/2""";

        var result = LinkExtractorService.ExtractUrls(html);

        result.Should().Contain("https://a.com/1");
        result.Should().Contain("https://b.com/2");
    }

    [Fact]
    public void ExtractUrls_EmptyHtml_ReturnsEmpty()
    {
        var result = LinkExtractorService.ExtractUrls("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractUrls_SingleQuotedHref_Extracts()
    {
        var html = "<a href='https://example.com/file.zip'>Download</a>";

        var result = LinkExtractorService.ExtractUrls(html);

        result.Should().Contain("https://example.com/file.zip");
    }

    // ── IsAllowedScheme (static) ──────────────────────────────

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("magnet:?xt=urn:btih:abc", true)]
    [InlineData("torrent://tracker.example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<h1>test</h1>", false)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("", false)]
    public void IsAllowedScheme_VariousSchemes_ReturnsExpected(string url, bool expected)
    {
        LinkExtractorService.IsAllowedScheme(url).Should().Be(expected);
    }

    // ── ParseSeasonEpisode (static) ──────────────────────────

    [Theory]
    [InlineData("https://example.com/show-S01E05.zip", 1, 5)]
    [InlineData("https://example.com/show-s12e03-720p", 12, 3)]
    [InlineData("https://example.com/show-S1E1", 1, 1)]
    public void ParseSeasonEpisode_FullPattern_ExtractsBoth(string url, int season, int episode)
    {
        var (s, e) = LinkExtractorService.ParseSeasonEpisode(url);

        s.Should().Be(season);
        e.Should().Be(episode);
    }

    [Fact]
    public void ParseSeasonEpisode_SeasonOnly_ExtractsSeason()
    {
        var (s, e) = LinkExtractorService.ParseSeasonEpisode("https://example.com/show-Season3");

        s.Should().Be(3);
        e.Should().BeNull();
    }

    [Fact]
    public void ParseSeasonEpisode_EpisodeOnly_ExtractsEpisode()
    {
        var (s, e) = LinkExtractorService.ParseSeasonEpisode("https://example.com/show-Episode10");

        s.Should().BeNull();
        e.Should().Be(10);
    }

    [Fact]
    public void ParseSeasonEpisode_NeitherPattern_ReturnsNull()
    {
        var (s, e) = LinkExtractorService.ParseSeasonEpisode("https://dl.host.com/film-2024.zip");

        s.Should().BeNull();
        e.Should().BeNull();
    }

    [Fact]
    public void ParseSeasonEpisode_EmptyUrl_ReturnsNull()
    {
        var (s, e) = LinkExtractorService.ParseSeasonEpisode("");

        s.Should().BeNull();
        e.Should().BeNull();
    }
}
